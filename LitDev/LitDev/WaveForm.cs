﻿//The following Copyright applies to the LitDev Extension for Small Basic and files in the namespace LitDev.
//Copyright (C) <2011 - 2015> litdev@hotmail.co.uk
//This file is part of the LitDev Extension for Small Basic.

//LitDev Extension is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//LitDev Extension is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with menu.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.SmallBasic.Library;
using SlimDX.DirectSound;
using SlimDX.Multimedia;
using SBArray = Microsoft.SmallBasic.Library.Array;
using System.Threading;

//Based on https://www.insecure.ws/2010/03/09/control-rc-aircrafts-from-your-computer-for-0

namespace LitDev
{
    /// <summary>
    /// Create PPM (Pulse Position Modulation) sound signals to control RC (remote control) devices.
    /// See http://blogs.msdn.com/b/smallbasic/archive/2014/05/10/smallbasic-pulse-position-modulation-extension.aspx.
    /// Additonally create simple sound waveforms.
    /// 
    /// SlimDX runtme for .Net 4.0 requires to be installed before this object can be used (http://slimdx.org/download.php).
    /// </summary>
    [SmallBasicType]
    public static class LDWaveForm
    {
        private static DirectSound directSound = null;
        private static WaveFormat waveFormat;
        private static short amplitude = 20262;
        private static bool bAsync = false;

        /// <summary>
        /// Play the sound asynchronously (return before sound completes), "True" or "False" default.
        /// </summary>
        public static Primitive Async
        {
            get { return bAsync; }
            set { bAsync = value; }
        }

        /// <summary>
        /// Signal amplitude (maximum is 2^15 = 32768, default is 20262)
        /// </summary>
        public static Primitive Amplitude
        {
            get { return (int)amplitude; }
            set { amplitude = (short)value; }
        }

        /// <summary>
        /// Play a Sine wave form.
        /// </summary>
        /// <param name="frequency">Frequency (HZ)</param>
        /// <param name="duration">Duration (ms)</param>
        public static void PlaySineWave(Primitive frequency, Primitive duration)
        {
            if (!VerifySlimDX.Verify()) return;

            Play(frequency, duration / 1000.0, 1);
        }

        /// <summary>
        /// Play a square wave form.
        /// </summary>
        /// <param name="frequency">Frequency (HZ)</param>
        /// <param name="duration">Duration (ms)</param>
        public static void PlaySquareWave(Primitive frequency, Primitive duration)
        {
            if (!VerifySlimDX.Verify()) return;

            Play(frequency, duration / 1000.0, 2);
        }

        /// <summary>
        /// Play a user defined wave form.
        /// </summary>
        /// <param name="frequency">Frequency (HZ)</param>
        /// <param name="duration">Duration (ms)</param>
        /// <param name="waveform">Form for the repeating wave.
        /// This is an array, where the index is an increasing relative time (the actual value is normalised to the frequency) and the value is an amplitude (-1 to 1).
        /// Example of a triangular wave would be "0=-1;1=1;2=-1;"</param>
        public static void PlayWave(Primitive frequency, Primitive duration, Primitive waveform)
        {
            if (!VerifySlimDX.Verify()) return;

            duration = duration / 1000.0; //seconds
            Initialise();
            try
            {
                int sampleCount = (int)(duration * waveFormat.SamplesPerSecond);

                // buffer description         
                SoundBufferDescription soundBufferDescription = new SoundBufferDescription();
                soundBufferDescription.Format = waveFormat;
                soundBufferDescription.Flags = BufferFlags.Defer;
                soundBufferDescription.SizeInBytes = sampleCount * waveFormat.BlockAlignment;

                SecondarySoundBuffer secondarySoundBuffer = new SecondarySoundBuffer(directSound, soundBufferDescription);

                short[] rawsamples = new short[sampleCount];
                double frac, value;

                Primitive indices = SBArray.GetAllIndices(waveform);
                int count = SBArray.GetItemCount(waveform);
                double interval = indices[count] - indices[1];
                double[] timeFrac = new double[count];
                double[] timeValue = new double[count];
                for (int i = 1; i <= count; i++) //Normalise to interval 1;
                {
                    timeFrac[i - 1] = (indices[i] - indices[1]) / interval;
                    timeValue[i - 1] = waveform[indices[i]];
                }

                for (int i = 0; i < sampleCount; i++)
                {
                    frac = frequency * duration * i / (double)sampleCount;
                    frac = frac - (int)frac;
                    for (int j = 0; j < count - 1; j++)
                    {
                        if (frac >= timeFrac[j] && frac <= timeFrac[j + 1])
                        {
                            value = timeValue[j] + (timeValue[j + 1] - timeValue[j]) * (frac - timeFrac[j]) / (timeFrac[j + 1] - timeFrac[j]);
                            rawsamples[i] = (short)(amplitude * value);
                            break;
                        }
                    }
                }

                Thread thread = new Thread(new ParameterizedThreadStart(DoPlay));
                thread.Start(new Object[] { rawsamples, secondarySoundBuffer });
                if (!bAsync) thread.Join();
            }
            catch (Exception ex)
            {
                Utilities.OnError(Utilities.GetCurrentMethod(), ex);
            }
        }

        /// <summary>
        /// Play DX7.
        /// </summary>
        /// <param name="channels">An array of values for each channel (values between 0 and 1, usually 8 channels).</param>
        public static void PlayDX7(Primitive channels)
        {
            if (!VerifySlimDX.Verify()) return;

            Initialise();
            try
            {
                int i, iServo;
                double duration = 0.0225;
                int sampleCount = (int)(duration * waveFormat.SamplesPerSecond);

                // buffer description         
                SoundBufferDescription soundBufferDescription = new SoundBufferDescription();
                soundBufferDescription.Format = waveFormat;
                soundBufferDescription.Flags = BufferFlags.Defer;
                soundBufferDescription.SizeInBytes = sampleCount * waveFormat.BlockAlignment;

                SecondarySoundBuffer secondarySoundBuffer = new SecondarySoundBuffer(directSound, soundBufferDescription);

                short[] rawsamples = new short[sampleCount];
                int stopSamples = (int)(0.0004 * waveFormat.SamplesPerSecond);
                List<int> servoSamples = new List<int>();
                Primitive indices = SBArray.GetAllIndices(channels);
                int servoCount = SBArray.GetItemCount(indices);
                for (iServo = 1; iServo <= servoCount; iServo++)
                {
                    servoSamples.Add((int)((0.0007 + 0.0008 * channels[indices[iServo]]) * waveFormat.SamplesPerSecond));
                }
                //Lead-in
                int leading = sampleCount - (servoCount + 1) * stopSamples - servoSamples.Sum();
                int sample = 0;
                for (i = 0; i < leading; i++) rawsamples[sample++] = 0;
                //Servos
                for (i = 0; i < stopSamples; i++) rawsamples[sample++] = (short)(-amplitude);
                for (iServo = 0; iServo < servoCount; iServo++)
                {
                    for (i = 0; i < servoSamples[iServo]; i++) rawsamples[sample++] = amplitude;
                    for (i = 0; i < stopSamples; i++) rawsamples[sample++] = (short)(-amplitude);
                }

                Thread thread = new Thread(new ParameterizedThreadStart(DoPlay));
                thread.Start(new Object[] { rawsamples, secondarySoundBuffer });
                if (!bAsync) thread.Join();
            }
            catch (Exception ex)
            {
                Utilities.OnError(Utilities.GetCurrentMethod(), ex);
            }
        }

        private static void DoPlay(Object obj)
        {
            short[] rawsamples = (short[])((Object[])obj)[0];
            SecondarySoundBuffer secondarySoundBuffer = (SecondarySoundBuffer)((Object[])obj)[1];

            //load audio samples to secondary buffer
            secondarySoundBuffer.Write(rawsamples, 0, LockFlags.EntireBuffer);

            //play audio buffer			
            secondarySoundBuffer.Play(0, PlayFlags.None);

            //wait to complete before returning
            while ((secondarySoundBuffer.Status & BufferStatus.Playing) != 0);

            secondarySoundBuffer.Dispose();
        }

        private static void Play(double frequency, double duration, int iType)
        {
            Initialise();
            try
            {
                int sampleCount = (int)(duration * waveFormat.SamplesPerSecond);

                // buffer description         
                SoundBufferDescription soundBufferDescription = new SoundBufferDescription();
                soundBufferDescription.Format = waveFormat;
                soundBufferDescription.Flags = BufferFlags.Defer;
                soundBufferDescription.SizeInBytes = sampleCount * waveFormat.BlockAlignment;

                SecondarySoundBuffer secondarySoundBuffer = new SecondarySoundBuffer(directSound, soundBufferDescription);

                short[] rawsamples = new short[sampleCount];
                double frac, value;

                switch (iType)
                {
                    case 1: //Sinusoidal
                        for (int i = 0; i < sampleCount; i++)
                        {
                            frac = frequency * duration * i / (double)sampleCount;
                            value = System.Math.Sin(2.0 * System.Math.PI * frac);
                            rawsamples[i] = (short)(amplitude * value);
                        }
                        break;
                    case 2: //Square
                        for (int i = 0; i < sampleCount; i++)
                        {
                            frac = frequency * duration * i / (double)sampleCount;
                            frac = frac - (int)frac;
                            value = frac < 0.5 ? -1.0 : 1.0;
                            rawsamples[i] = (short)(amplitude * value);
                        }
                        break;
                }

                Thread thread = new Thread(new ParameterizedThreadStart(DoPlay));
                thread.Start(new Object[] { rawsamples, secondarySoundBuffer });
                if (!bAsync) thread.Join();
            }
            catch (Exception ex)
            {
                Utilities.OnError(Utilities.GetCurrentMethod(), ex);
            }
        }

        private static void Initialise()
        {
            try
            {
                if (directSound == null)
                {
                    //Initialize the DirectSound Device
                    directSound = new DirectSound();

                    waveFormat = new WaveFormat();
                    waveFormat.BitsPerSample = 16;
                    waveFormat.Channels = 1;
                    waveFormat.BlockAlignment = (short)(waveFormat.BitsPerSample / 8);

                    waveFormat.FormatTag = WaveFormatTag.Pcm;
                    waveFormat.SamplesPerSecond = 192000;
                    waveFormat.AverageBytesPerSecond = waveFormat.SamplesPerSecond * waveFormat.BlockAlignment;
                }
                // Set the priority of the device with the rest of the operating system
                directSound.SetCooperativeLevel(User32.GetForegroundWindow(), CooperativeLevel.Priority);
            }
            catch (Exception ex)
            {
                Utilities.OnError(Utilities.GetCurrentMethod(), ex);
            }
        }
    }
}
