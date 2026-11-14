#pragma once

#include <cmath>

class Booster
{
public:
    static constexpr float Ceiling = 0.891f;

    void SetFormat(float framesPerSecond, unsigned channels)
    {
        m_channels = channels;
        m_gainStep = Coefficient(0.02f, framesPerSecond);
        m_release = Coefficient(0.15f, framesPerSecond);
        m_gain = 1.0f;
        m_reduction = 1.0f;
    }

    void Process(const float* source, float* destination, unsigned frames, float targetGain)
    {
        float gain = m_gain;
        float reduction = m_reduction;

        for (unsigned frame = 0; frame < frames; frame++)
        {
            gain += (targetGain - gain) * m_gainStep;

            float peak = 0.0f;

            for (unsigned channel = 0; channel < m_channels; channel++)
            {
                const float sample = source[channel] * gain;
                const float level = std::fabs(sample);

                destination[channel] = sample;

                if (level > peak)
                    peak = level;
            }

            const float wanted = peak > Ceiling ? Ceiling / peak : 1.0f;

            if (wanted < reduction)
                reduction = wanted;
            else
                reduction += (wanted - reduction) * m_release;

            if (reduction < 1.0f)
            {
                for (unsigned channel = 0; channel < m_channels; channel++)
                    destination[channel] *= reduction;
            }

            source += m_channels;
            destination += m_channels;
        }

        m_gain = gain;
        m_reduction = reduction;
    }

private:
    static float Coefficient(float seconds, float framesPerSecond)
    {
        return 1.0f - std::exp(-1.0f / (seconds * framesPerSecond));
    }

    unsigned m_channels = 1;
    float m_gainStep = 1.0f;
    float m_release = 1.0f;
    float m_gain = 1.0f;
    float m_reduction = 1.0f;
};
