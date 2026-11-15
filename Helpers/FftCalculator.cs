using System;

namespace DynamicIsland.Helpers
{
    public static class FftCalculator
    {
        /// <summary>
        /// Вычисляет БПФ (FFT) методом Кули-Тьюки (in-place) над массивом комплексных чисел
        /// </summary>
        public static void CalculateFft(float[] real, float[] imag)
        {
            int n = real.Length;
            if ((n & (n - 1)) != 0)
                throw new ArgumentException("Длина массива должна быть степенью двойки.", nameof(real));

            // Bit-reversal
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    (real[i], real[j]) = (real[j], real[i]);
                    (imag[i], imag[j]) = (imag[j], imag[i]);
                }
                int k = n >> 1;
                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            // Cooley-Tukey
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2.0 * Math.PI / len;
                float wlenReal = (float)Math.Cos(angle);
                float wlenImag = (float)Math.Sin(angle);

                int halfLen = len >> 1;
                for (int i = 0; i < n; i += len)
                {
                    float wReal = 1.0f;
                    float wImag = 0.0f;

                    for (int k = 0; k < halfLen; k++)
                    {
                        int uIdx = i + k;
                        int vIdx = i + k + halfLen;

                        float uReal = real[uIdx];
                        float uImag = imag[uIdx];

                        float vReal = real[vIdx] * wReal - imag[vIdx] * wImag;
                        float vImag = real[vIdx] * wImag + imag[vIdx] * wReal;

                        real[uIdx] = uReal + vReal;
                        imag[uIdx] = uImag + vImag;
                        real[vIdx] = uReal - vReal;
                        imag[vIdx] = uImag - vImag;

                        float nextWReal = wReal * wlenReal - wImag * wlenImag;
                        float nextWImag = wReal * wlenImag + wImag * wlenReal;
                        wReal = nextWReal;
                        wImag = nextWImag;
                    }
                }
            }
        }

        /// <summary>
        /// Применяет окно Ханна (Hann window) к входным данным для уменьшения утечки спектра
        /// </summary>
        public static void ApplyHannWindow(float[] buffer)
        {
            int length = buffer.Length;
            for (int i = 0; i < length; i++)
            {
                float window = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (length - 1)));
                buffer[i] *= window;
            }
        }

        /// <summary>
        /// Группирует магнитуды спектра в указанное количество частотных полос (логарифмически)
        /// </summary>
        public static float[] ExtractBands(float[] real, float[] imag, int bandCount)
        {
            int n = real.Length;
            int halfN = n / 2;
            float[] bands = new float[bandCount];

            // Вычисляем магнитуды спектра
            float[] magnitudes = new float[halfN];
            for (int i = 0; i < halfN; i++)
            {
                magnitudes[i] = (float)Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
            }

            // Логарифмическое распределение полос (от басов к высоким частотам)
            // Исключаем 0-й бин (DC offset)
            int startBin = 1;
            int endBin = halfN - 1;
            double logStart = Math.Log(startBin);
            double logEnd = Math.Log(endBin);

            for (int b = 0; b < bandCount; b++)
            {
                double binA = Math.Exp(logStart + (logEnd - logStart) * b / bandCount);
                double binB = Math.Exp(logStart + (logEnd - logStart) * (b + 1) / bandCount);

                int idxA = Math.Max(1, (int)Math.Floor(binA));
                int idxB = Math.Min(halfN - 1, (int)Math.Ceiling(binB));
                if (idxB <= idxA) idxB = idxA + 1;

                float sum = 0f;
                int count = 0;
                for (int i = idxA; i < idxB && i < halfN; i++)
                {
                    sum += magnitudes[i];
                    count++;
                }

                float avg = count > 0 ? sum / count : 0f;

                // Усиление высоких частот (pink/brown noise roll-off compensation)
                float boost = 1.0f + (float)b * 0.18f;
                float normalized = (float)Math.Sqrt(avg * boost * 0.05f);
                bands[b] = Math.Clamp(normalized, 0f, 1f);
            }

            return bands;
        }
    }
}
