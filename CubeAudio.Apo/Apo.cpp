#include <windows.h>
#include <sddl.h>
#include <atomic>
#include <cmath>
#include <new>
#include <audioenginebaseapo.h>
#include <baseaudioprocessingobject.h>
#include "Booster.h"
#include "Contract.h"

static const wchar_t ClassesKey[] = L"SOFTWARE\\Classes\\CLSID\\";
static const wchar_t RunningEventSecurity[] = L"D:(A;;0x00100000;;;WD)";

static HMODULE g_module;
static std::atomic<long> g_objects{ 0 };

namespace Settings
{
    static std::atomic<float> g_gain{ 1.0f };
    static SRWLOCK g_lock = SRWLOCK_INIT;
    static int g_users;
    static HANDLE g_thread;
    static HANDLE g_stop;

    static DWORD ReadValue(HKEY key, const wchar_t* name, DWORD fallback)
    {
        DWORD value = 0;
        DWORD size = sizeof(value);

        if (RegGetValueW(key, nullptr, name, RRF_RT_REG_DWORD, nullptr, &value, &size) != ERROR_SUCCESS)
            return fallback;

        return value;
    }

    static float ReadGain(HKEY key)
    {
        if (ReadValue(key, Contract::EnabledValue, 1) == 0)
            return 1.0f;

        DWORD tenths = ReadValue(key, Contract::GainValue, 0);

        if (tenths > Contract::MaxGainTenthsOfDb)
            tenths = Contract::MaxGainTenthsOfDb;

        return std::pow(10.0f, static_cast<float>(tenths) / 200.0f);
    }

    static DWORD WINAPI Watch(void*)
    {
        HKEY key;

        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, Contract::SettingsKey, 0, KEY_QUERY_VALUE | KEY_NOTIFY, &key) != ERROR_SUCCESS)
            return 0;

        const HANDLE changed = CreateEventW(nullptr, FALSE, FALSE, nullptr);

        if (changed != nullptr)
        {
            const HANDLE handles[] = { g_stop, changed };

            do
            {
                RegNotifyChangeKeyValue(key, FALSE, REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_THREAD_AGNOSTIC, changed, TRUE);
                g_gain.store(ReadGain(key), std::memory_order_relaxed);
            }
            while (WaitForMultipleObjects(2, handles, FALSE, INFINITE) == WAIT_OBJECT_0 + 1);

            CloseHandle(changed);
        }

        RegCloseKey(key);
        return 0;
    }

    static void Acquire()
    {
        AcquireSRWLockExclusive(&g_lock);

        if (g_users++ == 0)
        {
            g_stop = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            g_thread = g_stop != nullptr ? CreateThread(nullptr, 0, Watch, nullptr, 0, nullptr) : nullptr;
        }

        ReleaseSRWLockExclusive(&g_lock);
    }

    static void Release()
    {
        AcquireSRWLockExclusive(&g_lock);

        if (--g_users == 0)
        {
            if (g_thread != nullptr)
            {
                SetEvent(g_stop);
                WaitForSingleObject(g_thread, INFINITE);
                CloseHandle(g_thread);
                g_thread = nullptr;
            }

            if (g_stop != nullptr)
            {
                CloseHandle(g_stop);
                g_stop = nullptr;
            }

            g_gain.store(1.0f, std::memory_order_relaxed);
        }

        ReleaseSRWLockExclusive(&g_lock);
    }
}

#pragma warning(push)
#pragma warning(disable: 4815)
static const CRegAPOProperties<1> g_registration(
    Contract::ClassGuid, L"CubeAudio", L"", 1, 0, __uuidof(IAudioSystemEffects),
    static_cast<APO_FLAG>(APO_FLAG_INPLACE | APO_FLAG_SAMPLESPERFRAME_MUST_MATCH | APO_FLAG_FRAMESPERSECOND_MUST_MATCH | APO_FLAG_BITSPERSAMPLE_MUST_MATCH));
#pragma warning(pop)

struct INonDelegatingUnknown
{
    virtual HRESULT STDMETHODCALLTYPE NonDelegatingQueryInterface(REFIID iid, void** object) = 0;
    virtual ULONG STDMETHODCALLTYPE NonDelegatingAddRef() = 0;
    virtual ULONG STDMETHODCALLTYPE NonDelegatingRelease() = 0;
};

class CubeAudioApo final : public CBaseAudioProcessingObject, public IAudioSystemEffects, public INonDelegatingUnknown
{
public:
    explicit CubeAudioApo(IUnknown* outer) : CBaseAudioProcessingObject(g_registration)
    {
        m_outer = outer != nullptr ? outer : reinterpret_cast<IUnknown*>(static_cast<INonDelegatingUnknown*>(this));

        g_objects++;
        Settings::Acquire();
    }

    ~CubeAudioApo() override
    {
        ShowRunning(false);
        Settings::Release();
        g_objects--;
    }

    STDMETHODIMP QueryInterface(REFIID iid, void** object) override
    {
        return m_outer->QueryInterface(iid, object);
    }

    STDMETHODIMP_(ULONG) AddRef() override
    {
        return m_outer->AddRef();
    }

    STDMETHODIMP_(ULONG) Release() override
    {
        return m_outer->Release();
    }

    HRESULT STDMETHODCALLTYPE NonDelegatingQueryInterface(REFIID iid, void** object) override
    {
        if (object == nullptr)
            return E_POINTER;

        if (iid == __uuidof(IUnknown))
            *object = static_cast<INonDelegatingUnknown*>(this);
        else if (iid == __uuidof(IAudioProcessingObject))
            *object = static_cast<IAudioProcessingObject*>(this);
        else if (iid == __uuidof(IAudioProcessingObjectRT))
            *object = static_cast<IAudioProcessingObjectRT*>(this);
        else if (iid == __uuidof(IAudioProcessingObjectConfiguration))
            *object = static_cast<IAudioProcessingObjectConfiguration*>(this);
        else if (iid == __uuidof(IAudioSystemEffects))
            *object = static_cast<IAudioSystemEffects*>(this);
        else
        {
            *object = nullptr;
            return E_NOINTERFACE;
        }

        reinterpret_cast<IUnknown*>(*object)->AddRef();
        return S_OK;
    }

    ULONG STDMETHODCALLTYPE NonDelegatingAddRef() override
    {
        return static_cast<ULONG>(++m_references);
    }

    ULONG STDMETHODCALLTYPE NonDelegatingRelease() override
    {
        const long references = --m_references;

        if (references == 0)
            delete this;

        return static_cast<ULONG>(references);
    }

    STDMETHODIMP Initialize(UINT32 size, BYTE* data) override
    {
        if ((data == nullptr) != (size == 0))
            return E_INVALIDARG;

        if (m_bIsInitialized)
            return APOERR_ALREADY_INITIALIZED;

        m_bIsInitialized = true;
        return S_OK;
    }

    STDMETHODIMP LockForProcess(UINT32 inputCount, APO_CONNECTION_DESCRIPTOR** inputs, UINT32 outputCount, APO_CONNECTION_DESCRIPTOR** outputs) override
    {
        const HRESULT result = CBaseAudioProcessingObject::LockForProcess(inputCount, inputs, outputCount, outputs);

        if (SUCCEEDED(result))
        {
            m_booster.SetFormat(GetFramesPerSecond(), GetSamplesPerFrame());
            ShowRunning(true);
        }

        return result;
    }

    STDMETHODIMP UnlockForProcess() override
    {
        ShowRunning(false);

        return CBaseAudioProcessingObject::UnlockForProcess();
    }

    STDMETHODIMP_(void) APOProcess(UINT32, APO_CONNECTION_PROPERTY** inputs, UINT32, APO_CONNECTION_PROPERTY** outputs) override
    {
        const APO_CONNECTION_PROPERTY* input = inputs[0];
        APO_CONNECTION_PROPERTY* output = outputs[0];

        if (input->u32BufferFlags == BUFFER_VALID)
        {
            m_booster.Process(
                reinterpret_cast<const float*>(input->pBuffer), reinterpret_cast<float*>(output->pBuffer), input->u32ValidFrameCount,
                Settings::g_gain.load(std::memory_order_relaxed));
        }

        output->u32ValidFrameCount = input->u32ValidFrameCount;
        output->u32BufferFlags = input->u32BufferFlags;
    }

private:
    void ShowRunning(bool running)
    {
        if (running && m_running == nullptr)
        {
            SECURITY_ATTRIBUTES attributes = { sizeof(attributes), nullptr, FALSE };
            ConvertStringSecurityDescriptorToSecurityDescriptorW(RunningEventSecurity, SDDL_REVISION_1, &attributes.lpSecurityDescriptor, nullptr);

            m_running = CreateEventW(&attributes, TRUE, FALSE, Contract::RunningEvent);
            LocalFree(attributes.lpSecurityDescriptor);
        }
        else if (!running && m_running != nullptr)
        {
            CloseHandle(m_running);
            m_running = nullptr;
        }
    }

    IUnknown* m_outer;
    std::atomic<long> m_references{ 1 };
    HANDLE m_running = nullptr;
    Booster m_booster;
};

class ClassFactory final : public IClassFactory
{
public:
    STDMETHODIMP QueryInterface(REFIID iid, void** object) override
    {
        if (object == nullptr)
            return E_POINTER;

        if (iid != __uuidof(IUnknown) && iid != __uuidof(IClassFactory))
        {
            *object = nullptr;
            return E_NOINTERFACE;
        }

        *object = static_cast<IClassFactory*>(this);
        return S_OK;
    }

    STDMETHODIMP_(ULONG) AddRef() override { return 2; }
    STDMETHODIMP_(ULONG) Release() override { return 1; }

    STDMETHODIMP CreateInstance(IUnknown* outer, REFIID iid, void** object) override
    {
        if (object == nullptr)
            return E_POINTER;

        *object = nullptr;

        if (outer != nullptr && iid != __uuidof(IUnknown))
            return CLASS_E_NOAGGREGATION;

        CubeAudioApo* apo = new (std::nothrow) CubeAudioApo(outer);

        if (apo == nullptr)
            return E_OUTOFMEMORY;

        const HRESULT result = apo->NonDelegatingQueryInterface(iid, object);
        apo->NonDelegatingRelease();

        return result;
    }

    STDMETHODIMP LockServer(BOOL lock) override
    {
        g_objects += lock ? 1 : -1;
        return S_OK;
    }
};

static ClassFactory g_factory;

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, void*)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID iid, void** object)
{
    if (clsid != Contract::ClassGuid)
        return CLASS_E_CLASSNOTAVAILABLE;

    return g_factory.QueryInterface(iid, object);
}

STDAPI DllCanUnloadNow()
{
    return g_objects == 0 ? S_OK : S_FALSE;
}

static HRESULT SetString(const wchar_t* subKey, const wchar_t* name, const wchar_t* value)
{
    const DWORD size = static_cast<DWORD>((wcslen(value) + 1) * sizeof(wchar_t));

    return HRESULT_FROM_WIN32(RegSetKeyValueW(HKEY_LOCAL_MACHINE, subKey, name, REG_SZ, value, size));
}

static void ClassKey(wchar_t (&key)[MAX_PATH], const wchar_t* subKey = L"")
{
    wcscpy_s(key, ClassesKey);
    wcscat_s(key, Contract::ClassId);
    wcscat_s(key, subKey);
}

STDAPI DllRegisterServer()
{
    wchar_t path[MAX_PATH];
    const DWORD length = GetModuleFileNameW(g_module, path, MAX_PATH);

    if (length == 0 || length == MAX_PATH)
        return E_FAIL;

    wchar_t key[MAX_PATH];
    wchar_t server[MAX_PATH];
    ClassKey(key);
    ClassKey(server, L"\\InprocServer32");

    HRESULT result = SetString(key, nullptr, L"CubeAudio");

    if (SUCCEEDED(result))
        result = SetString(server, nullptr, path);

    if (SUCCEEDED(result))
        result = SetString(server, L"ThreadingModel", L"Both");

    if (SUCCEEDED(result))
        result = RegisterAPO(g_registration);

    return result;
}

STDAPI DllUnregisterServer()
{
    UnregisterAPO(Contract::ClassGuid);

    wchar_t key[MAX_PATH];
    ClassKey(key);

    const LSTATUS status = RegDeleteTreeW(HKEY_LOCAL_MACHINE, key);

    if (status == ERROR_SUCCESS)
        RegDeleteKeyW(HKEY_LOCAL_MACHINE, key);

    return status == ERROR_SUCCESS || status == ERROR_FILE_NOT_FOUND ? S_OK : HRESULT_FROM_WIN32(status);
}
