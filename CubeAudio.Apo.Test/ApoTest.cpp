#include <windows.h>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <vector>
#include <audioenginebaseapo.h>
#include <audiomediatype.h>
#include <ks.h>
#include <ksmedia.h>
#include "Booster.h"

static const CLSID CLSID_CubeAudioApo = { 0x31b5ee6d, 0x6025, 0x41f0, { 0xbd, 0xcd, 0x1f, 0xa5, 0x0b, 0xc6, 0xdb, 0x9f } };

static int g_failures;

static void Check(bool ok, const char* what)
{
    printf("%s  %s\n", ok ? "ok  " : "FAIL", what);

    if (!ok)
        g_failures++;
}

static std::vector<float> Sine(unsigned frames, unsigned channels, float amplitude, float framesPerSecond)
{
    std::vector<float> samples(frames * channels);

    for (unsigned frame = 0; frame < frames; frame++)
    {
        const float value = amplitude * std::sin(6.2831853f * 220.0f * frame / framesPerSecond);

        for (unsigned channel = 0; channel < channels; channel++)
            samples[(frame * channels) + channel] = value;
    }

    return samples;
}

static float Peak(const std::vector<float>& samples, size_t from)
{
    float peak = 0.0f;

    for (size_t i = from; i < samples.size(); i++)
        peak = std::max(peak, std::fabs(samples[i]));

    return peak;
}

static void TestBooster()
{
    const float rate = 96000.0f;

    {
        Booster booster;
        booster.SetFormat(rate, 1);

        std::vector<float> input = Sine(96000, 1, 0.0316228f, rate);
        std::vector<float> output(input.size());
        booster.Process(input.data(), output.data(), 96000, 10.0f);

        const float peak = Peak(output, 48000);
        Check(std::fabs(peak - 0.316228f) < 0.002f, "+20 dB on a quiet sine is exactly +20 dB");
        Check(std::fabs(output[10]) < std::fabs(input[10]) * 1.2f, "gain fades in instead of jumping");
    }

    {
        Booster booster;
        booster.SetFormat(rate, 2);

        std::vector<float> samples = Sine(96000, 2, 0.5f, rate);
        booster.Process(samples.data(), samples.data(), 96000, 31.6228f);

        Check(Peak(samples, 0) <= Booster::Ceiling + 1e-6f, "limiter never lets a sample over the ceiling (stereo, in place)");
        Check(Peak(samples, 48000) > Booster::Ceiling * 0.9f, "limiter holds the signal at the ceiling, not below");
    }

    {
        Booster booster;
        booster.SetFormat(rate, 1);

        std::vector<float> loud = Sine(9600, 1, 0.5f, rate);
        std::vector<float> quiet = Sine(96000, 1, 0.01f, rate);
        booster.Process(loud.data(), loud.data(), 9600, 10.0f);
        booster.Process(quiet.data(), quiet.data(), 96000, 10.0f);

        Check(std::fabs(Peak(quiet, 86400) - 0.1f) < 0.002f, "limiter lets go again after a loud burst");
    }

    {
        Booster booster;
        booster.SetFormat(rate, 1);

        std::vector<float> input = Sine(4800, 1, 0.25f, rate);
        std::vector<float> output(input.size());
        booster.Process(input.data(), output.data(), 4800, 1.0f);

        Check(output == input, "0 dB is bit exact");
    }
}

class Outer final : public IUnknown
{
public:
    IUnknown* inner = nullptr;
    long references = 1;

    STDMETHODIMP QueryInterface(REFIID iid, void** object) override
    {
        if (iid != __uuidof(IUnknown))
            return inner->QueryInterface(iid, object);

        *object = static_cast<IUnknown*>(this);
        references++;
        return S_OK;
    }

    STDMETHODIMP_(ULONG) AddRef() override { return static_cast<ULONG>(++references); }
    STDMETHODIMP_(ULONG) Release() override { return static_cast<ULONG>(--references); }
};

static void TestAggregation(IClassFactory* factory)
{
    Outer outer;
    void* refused = nullptr;

    Check(factory->CreateInstance(&outer, __uuidof(IAudioProcessingObject), &refused) == CLASS_E_NOAGGREGATION, "aggregating is only possible through IUnknown");

    const HRESULT created = factory->CreateInstance(&outer, __uuidof(IUnknown), reinterpret_cast<void**>(&outer.inner));
    Check(SUCCEEDED(created), "can be aggregated into an outer object, the way the audio engine does it");

    if (FAILED(created))
        return;

    Check(outer.references == 1, "creating the inner object doesn't touch the outer one");

    IAudioProcessingObjectRT* realtime = nullptr;
    Check(SUCCEEDED(outer.QueryInterface(__uuidof(IAudioProcessingObjectRT), reinterpret_cast<void**>(&realtime))), "outer object exposes the APO interfaces");
    Check(outer.references == 2, "APO interfaces count their references on the outer object");

    IUnknown* identity = nullptr;
    Check(SUCCEEDED(realtime->QueryInterface(__uuidof(IUnknown), reinterpret_cast<void**>(&identity))) && identity == &outer, "IUnknown of an APO interface is the outer object");

    if (identity != nullptr)
        identity->Release();

    realtime->Release();
    Check(outer.references == 1, "releasing the interfaces gives the references back");
    Check(outer.inner->Release() == 0, "releasing the inner IUnknown destroys the APO");
}

static void TestApo(const wchar_t* path)
{
    const HMODULE module = LoadLibraryW(path);
    Check(module != nullptr, "DLL loads");

    if (module == nullptr)
        return;

    typedef HRESULT(STDAPICALLTYPE* GetClassObject)(REFCLSID, REFIID, void**);
    typedef HRESULT(STDAPICALLTYPE* CanUnloadNow)();

    const GetClassObject getClassObject = reinterpret_cast<GetClassObject>(GetProcAddress(module, "DllGetClassObject"));
    const CanUnloadNow canUnloadNow = reinterpret_cast<CanUnloadNow>(GetProcAddress(module, "DllCanUnloadNow"));
    Check(getClassObject != nullptr && canUnloadNow != nullptr && GetProcAddress(module, "DllRegisterServer") != nullptr, "DLL exports the COM entry points");

    IClassFactory* factory = nullptr;
    Check(SUCCEEDED(getClassObject(CLSID_CubeAudioApo, __uuidof(IClassFactory), reinterpret_cast<void**>(&factory))), "class factory");

    IAudioProcessingObject* apo = nullptr;
    Check(SUCCEEDED(factory->CreateInstance(nullptr, __uuidof(IAudioProcessingObject), reinterpret_cast<void**>(&apo))), "create APO");

    IAudioProcessingObjectRT* realtime = nullptr;
    IAudioProcessingObjectConfiguration* configuration = nullptr;
    IAudioSystemEffects* effects = nullptr;
    Check(SUCCEEDED(apo->QueryInterface(__uuidof(IAudioProcessingObjectRT), reinterpret_cast<void**>(&realtime))), "IAudioProcessingObjectRT");
    Check(SUCCEEDED(apo->QueryInterface(__uuidof(IAudioProcessingObjectConfiguration), reinterpret_cast<void**>(&configuration))), "IAudioProcessingObjectConfiguration");
    Check(SUCCEEDED(apo->QueryInterface(__uuidof(IAudioSystemEffects), reinterpret_cast<void**>(&effects))), "IAudioSystemEffects");

    APO_REG_PROPERTIES* registration = nullptr;
    Check(SUCCEEDED(apo->GetRegistrationProperties(&registration)) && registration->clsid == CLSID_CubeAudioApo, "registration properties");
    CoTaskMemFree(registration);

    APOInitSystemEffects2 init = {};
    init.APOInit.cbSize = sizeof(init);
    init.APOInit.clsid = CLSID_CubeAudioApo;
    Check(SUCCEEDED(apo->Initialize(sizeof(init), reinterpret_cast<BYTE*>(&init))), "Initialize");

    UNCOMPRESSEDAUDIOFORMAT format = {};
    format.guidFormatType = KSDATAFORMAT_SUBTYPE_IEEE_FLOAT;
    format.dwSamplesPerFrame = 1;
    format.dwBytesPerSampleContainer = 4;
    format.dwValidBitsPerSample = 32;
    format.fFramesPerSecond = 96000.0f;
    format.dwChannelMask = KSAUDIO_SPEAKER_MONO;

    IAudioMediaType* type = nullptr;
    Check(SUCCEEDED(CreateAudioMediaTypeFromUncompressedAudioFormat(&format, &type)), "media type");

    IAudioMediaType* supported = nullptr;
    Check(apo->IsInputFormatSupported(nullptr, type, &supported) == S_OK, "accepts 96 kHz mono float as input");
    if (supported != nullptr)
        supported->Release();

    supported = nullptr;
    Check(apo->IsOutputFormatSupported(type, type, &supported) == S_OK, "accepts the same format as output");
    if (supported != nullptr)
        supported->Release();

    const unsigned frames = 960;
    std::vector<float> input = Sine(frames, 1, 0.1f, 96000.0f);
    std::vector<float> output(frames, 123.0f);

    APO_CONNECTION_DESCRIPTOR inputDescriptor = { APO_CONNECTION_BUFFER_TYPE_EXTERNAL, reinterpret_cast<UINT_PTR>(input.data()), frames, type, APO_CONNECTION_DESCRIPTOR_SIGNATURE };
    APO_CONNECTION_DESCRIPTOR outputDescriptor = { APO_CONNECTION_BUFFER_TYPE_EXTERNAL, reinterpret_cast<UINT_PTR>(output.data()), frames, type, APO_CONNECTION_DESCRIPTOR_SIGNATURE };
    APO_CONNECTION_DESCRIPTOR* inputDescriptors[] = { &inputDescriptor };
    APO_CONNECTION_DESCRIPTOR* outputDescriptors[] = { &outputDescriptor };

    const HRESULT locked = configuration->LockForProcess(1, inputDescriptors, 1, outputDescriptors);
    Check(SUCCEEDED(locked), "LockForProcess");

    if (SUCCEEDED(locked))
    {
        APO_CONNECTION_PROPERTY inputProperty = { reinterpret_cast<UINT_PTR>(input.data()), frames, BUFFER_VALID, APO_CONNECTION_PROPERTY_SIGNATURE };
        APO_CONNECTION_PROPERTY outputProperty = { reinterpret_cast<UINT_PTR>(output.data()), 0, BUFFER_INVALID, APO_CONNECTION_PROPERTY_SIGNATURE };
        APO_CONNECTION_PROPERTY* inputProperties[] = { &inputProperty };
        APO_CONNECTION_PROPERTY* outputProperties[] = { &outputProperty };

        Sleep(200);

        for (int warmUp = 0; warmUp < 50; warmUp++)
            realtime->APOProcess(1, inputProperties, 1, outputProperties);

        outputProperty.u32BufferFlags = BUFFER_INVALID;
        realtime->APOProcess(1, inputProperties, 1, outputProperties);
        Check(outputProperty.u32BufferFlags == BUFFER_VALID && outputProperty.u32ValidFrameCount == frames, "APOProcess marks the output valid");

        float ratio = 0.0f;
        bool sameShape = true;

        for (unsigned frame = 0; frame < frames; frame++)
        {
            if (std::fabs(input[frame]) < 0.01f)
                continue;

            const float current = output[frame] / input[frame];

            if (ratio == 0.0f)
                ratio = current;

            sameShape = sameShape && std::fabs(current - ratio) < (ratio * 0.02f);
        }

        Check(sameShape && ratio >= 0.99f, "APOProcess output is the input, times the configured gain");
        printf("      gain seen through the DLL: %+.1f dB\n", 20.0f * std::log10(ratio));

        inputProperty.u32BufferFlags = BUFFER_SILENT;
        realtime->APOProcess(1, inputProperties, 1, outputProperties);
        Check(outputProperty.u32BufferFlags == BUFFER_SILENT, "silence stays flagged as silence");

        Check(SUCCEEDED(configuration->UnlockForProcess()), "UnlockForProcess");
    }

    type->Release();
    effects->Release();
    configuration->Release();
    realtime->Release();

    Check(canUnloadNow() == S_FALSE, "DLL stays loaded while an APO exists");
    Check(apo->Release() == 0, "last Release destroys the APO");
    Check(canUnloadNow() == S_OK, "DLL can unload afterwards (settings thread stopped)");

    TestAggregation(factory);
    Check(canUnloadNow() == S_OK, "DLL can unload after the aggregated APO is gone");

    FreeLibrary(module);
}

int wmain(int argc, wchar_t** argv)
{
    TestBooster();

    if (argc > 1)
        TestApo(argv[1]);

    printf(g_failures == 0 ? "\nall passed\n" : "\n%d failed\n", g_failures);
    return g_failures == 0 ? 0 : 1;
}
