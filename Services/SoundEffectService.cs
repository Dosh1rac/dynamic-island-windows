using System;
using System.IO;
using System.Media;

namespace DynamicIsland.Services
{
    public interface ISoundEffectService
    {
        bool IsSoundEnabled { get; set; }
        void PlayPop();
        void PlaySnap();
        void PlayDrop();
    }

    public class SoundEffectService : ISoundEffectService
    {
        public bool IsSoundEnabled { get; set; } = true;

        private byte[]? _popWav;
        private byte[]? _snapWav;
        private byte[]? _dropWav;

        public SoundEffectService()
        {
            // Предварительная генерация WAV в памяти для моментального отклика
            _popWav = GenerateToneWav(frequencyStart: 650, frequencyEnd: 240, durationMs: 45, volume: 0.25f);
            _snapWav = GenerateToneWav(frequencyStart: 1100, frequencyEnd: 450, durationMs: 25, volume: 0.18f);
            _dropWav = GenerateToneWav(frequencyStart: 350, frequencyEnd: 700, durationMs: 50, volume: 0.22f);
        }

        public void PlayPop()
        {
            if (!IsSoundEnabled || _popWav == null) return;
            PlayBytes(_popWav);
        }

        public void PlaySnap()
        {
            if (!IsSoundEnabled || _snapWav == null) return;
            PlayBytes(_snapWav);
        }

        public void PlayDrop()
        {
            if (!IsSoundEnabled || _dropWav == null) return;
            PlayBytes(_dropWav);
        }

        private static void PlayBytes(byte[] wavData)
        {
            try
            {
                using var ms = new MemoryStream(wavData);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
            catch
            {
            }
        }

        private static byte[] GenerateToneWav(float frequencyStart, float frequencyEnd, int durationMs, float volume)
        {
            int sampleRate = 44100;
            int samples = (sampleRate * durationMs) / 1000;
            short[] pcm = new short[samples];

            double phase = 0.0;
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / samples;
                double freq = frequencyStart + (frequencyEnd - frequencyStart) * t;
                phase += 2.0 * Math.PI * freq / sampleRate;

                // Экспоненциальное затухание (envelope decay)
                double env = Math.Exp(-4.5 * t);
                pcm[i] = (short)(Math.Sin(phase) * env * volume * 32767);
            }

            return PackPcmToWav(pcm, sampleRate);
        }

        private static byte[] PackPcmToWav(short[] pcm, int sampleRate)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            int subChunk2Size = pcm.Length * 2;
            int chunkSize = 36 + subChunk2Size;

            bw.Write(new char[4] { 'R', 'I', 'F', 'F' });
            bw.Write(chunkSize);
            bw.Write(new char[4] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[4] { 'f', 'm', 't', ' ' });
            bw.Write(16); // subchunk1size (PCM)
            bw.Write((short)1); // audio format (PCM)
            bw.Write((short)1); // channels (mono)
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2); // byte rate
            bw.Write((short)2); // block align
            bw.Write((short)16); // bits per sample
            bw.Write(new char[4] { 'd', 'a', 't', 'a' });
            bw.Write(subChunk2Size);

            for (int i = 0; i < pcm.Length; i++)
            {
                bw.Write(pcm[i]);
            }

            return ms.ToArray();
        }
    }
}
