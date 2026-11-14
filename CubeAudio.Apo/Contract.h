#pragma once

#include <guiddef.h>

namespace Contract
{
    inline constexpr wchar_t ClassId[] = L"{31B5EE6D-6025-41F0-BDCD-1FA50BC6DB9F}";
    inline constexpr wchar_t SettingsKey[] = L"SOFTWARE\\CubeAudio";
    inline constexpr wchar_t EnabledValue[] = L"Enabled";
    inline constexpr wchar_t GainValue[] = L"GainTenthsOfDb";
    inline constexpr wchar_t RunningEvent[] = L"Global\\CubeAudioApoRunning";
    inline constexpr unsigned long MaxGainTenthsOfDb = 300;

    constexpr unsigned long Hex(const wchar_t* text, int digits)
    {
        unsigned long value = 0;

        for (int i = 0; i < digits; i++)
        {
            const wchar_t c = text[i];
            value = (value << 4) | static_cast<unsigned long>(c <= L'9' ? c - L'0' : (c | 0x20) - L'a' + 10);
        }

        return value;
    }

    constexpr GUID ParseGuid(const wchar_t* text)
    {
        return {
            Hex(text + 1, 8),
            static_cast<unsigned short>(Hex(text + 10, 4)),
            static_cast<unsigned short>(Hex(text + 15, 4)),
            {
                static_cast<unsigned char>(Hex(text + 20, 2)), static_cast<unsigned char>(Hex(text + 22, 2)),
                static_cast<unsigned char>(Hex(text + 25, 2)), static_cast<unsigned char>(Hex(text + 27, 2)),
                static_cast<unsigned char>(Hex(text + 29, 2)), static_cast<unsigned char>(Hex(text + 31, 2)),
                static_cast<unsigned char>(Hex(text + 33, 2)), static_cast<unsigned char>(Hex(text + 35, 2))
            }
        };
    }

    inline constexpr GUID ClassGuid = ParseGuid(ClassId);
}
