﻿using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EKG_Project.IO;
using EKG_Project.Modules.ECG_Baseline;


namespace EKG_Project.Modules.Sleep_Apnea
{
    #region Sleep_Apnea Class doc
    /// <summary>
    /// Class that locates the periods of sleep apnea in ECG 
    /// </summary>
    #endregion

    public class Sleep_Apnea_Alg
    {

        #region
        /// <summary>
        /// Function that finds intervals between RR peaks
        /// </summary>
        /// <param name="freq"> frequency of sampling for signal</param>
        /// <param name="R_detected"> Signal - number of samples for R peaks </param>
        /// <returns> RR intervals</returns>
        #endregion
        
        List<List<double>> findIntervals(List<uint> R_detected, int freq)
        {
            var timeInSec = new List<double>(R_detected.Count-1);
            var rrDist = new List<double>(R_detected.Count-1);
            new List<double>(R_detected.Count-1);

            for (int i = 0; i < R_detected.Count()- 1; i++)
            {
                timeInSec.Add(((double)R_detected[i]) / freq);
                rrDist.Add(((double)R_detected[i + 1] - R_detected[i]) / freq);
            }

            List<List<double>> RR = new List<List<double>>(2);
            RR.Add(timeInSec);
            RR.Add(rrDist);
            return RR;
        }

        #region
        /// <summary>
        /// Function that filters RR intervals using average filter
        /// </summary>
        /// <param name="RR"> Signal - RR intervals </param>
        /// <returns> Filtered RR intervals</returns>
        #endregion

        List<List<double>> averageFilter(List<List<double>> RR)
        {
            int meanFilterWindowLength = 41;

            List<double> rrDistFiltered = new List<double>(RR[1].Count);
            List<double> timeInSecFiltered = new List<double>(RR[0].Count);

            for (int i = 0; i < RR[1].Count - meanFilterWindowLength; i++)
            {
                List<double> meanWindow = RR[1].GetRange(i, meanFilterWindowLength);

                double sum = 0;
                int counter = 0;
                for(int j = 0 ; j < meanFilterWindowLength ; j++)
                {
                    if(meanWindow[j] > 0.4 && meanWindow[j] < 2.0 && j != meanFilterWindowLength/2)
                    {
                        sum = sum + meanWindow[j];
                        counter++;
                    }
                }

                int currentIndex = meanFilterWindowLength / 2 + i;
                double currentMean = sum / counter;
                if (currentMean * 0.8 < RR[1][currentIndex] && currentMean * 1.2 > RR[1][currentIndex])
                {
                    rrDistFiltered.Add(RR[1][currentIndex]);
                    timeInSecFiltered.Add(RR[0][currentIndex]);
                }              
            }

            List<List<double>> RRFiltered = new List<List<double>>(2);
            RRFiltered.Add(timeInSecFiltered);
            RRFiltered.Add(rrDistFiltered);

            return RRFiltered;
        }

        #region
        /// <summary>
        /// Function that resamples RR intervals on 1Hz frequency
        /// </summary>
        /// <param name="freq"> frequency of sampling for signal</param>
        /// <param name="RRFiltered"> Signal - RR intervals filtered </param>
        /// <returns> Resampled RR intervals</returns>
        #endregion   
        List<List<double>> resampling(List<List<double>> RRFiltered, int resampFreq)
        {
            int estimatedSamplesCountAfterResampling = (int)((RRFiltered[0][RRFiltered[0].Count-1] - RRFiltered[0][0]) * resampFreq + 1);
            List<double> rrDistResampled = new List<double>(estimatedSamplesCountAfterResampling);
            List<double> timeInSecResampled = new List<double>(estimatedSamplesCountAfterResampling);

            double x = RRFiltered[0][0];
            for (int i = 0; i < RRFiltered[0].Count-1; i++)
            {
                double x1 = RRFiltered[0][i];
                double x2 = RRFiltered[0][i+1];
                double y1 = RRFiltered[1][i];
                double y2 = RRFiltered[1][i+1];

                double a = (y1 - y2) / (x1 - x2);
                double b = y1 - a * x1;

                while (x < x2)
                {
                    timeInSecResampled.Add(x);
                    rrDistResampled.Add(a * x + b);
                    x += (1.0 / resampFreq);
                }
            }

            List<List<double>> RRResampled = new List<List<double>>(2);
            RRResampled.Add(timeInSecResampled);
            RRResampled.Add(rrDistResampled);

            return RRResampled;
        }

        private List<List<double>> LP(List<List<double>> RRHP)
        {
            List<double> rrDistLP = new List<double>(RRHP[1].Count);
            List<double> timeInSecLP = new List<double>(RRHP[0].Count);

            int LPFilterWindowLength = 5;
            double sum = 0;
            int filterIndex = 0;
            double[] LPFilterWindow = new double[LPFilterWindowLength];

            for (int i = 0; i < RRHP[1].Count; i++)
            {
                LPFilterWindow[filterIndex] = RRHP[1][i];
                sum += RRHP[1][i];
                filterIndex++;

                if(i < LPFilterWindowLength)
                {
                    continue;
                }

                double newValue = sum / LPFilterWindowLength;
                rrDistLP.Add(newValue);
                timeInSecLP.Add(RRHP[0][i-(LPFilterWindowLength/2)]);

                if(filterIndex >= LPFilterWindowLength)
                {
                    filterIndex = 0;
                }

                sum -= LPFilterWindow[filterIndex];
            }

            List<List<double>> RRLP = new List<List<double>>(2);
            RRLP.Add(timeInSecLP);
            RRLP.Add(rrDistLP);

            return RRLP;
        }

        private List<List<double>> HP(List<List<double>> RRResampled)
        {
            List<double> rrDistHP = new List<double>(RRResampled[1].Count);

            double cutoff = 0.01;
            double RC = 1.0 / (cutoff * 2 * Math.PI);
            double dt = 1.0;
            double alpha = RC / (RC + dt);
            double prevValue = 0;

            for (int i = 1; i < RRResampled[1].Count; i++)
            {
                prevValue = alpha * (prevValue + RRResampled[1][i] - RRResampled[1][i - 1]);
                rrDistHP.Add(prevValue);
            }

            List<List<double>> RRHP = new List<List<double>>(2);
            RRHP.Add(RRResampled[0].GetRange(1, RRResampled[0].Count-1));
            RRHP.Add(rrDistHP);
            return RRHP;
        }


        #region
        /// <summary>
        /// Function that creates Hilbert transform for signal
        /// </summary>
        /// <param name="RRHPLP"> Signal - RR intervals filtered </param>
        /// <returns> Hilbert's amplitudes and frequencies </returns>
        #endregion

        void hilbert(List<List<double>> RRHPLP, ref List<List<double>> hAmp, ref List<List<double>> hFreq)
        {
            Complex[] hilb = MatlabHilbert(RRHPLP[1].ToArray());

            double Fs = 1.0 / (RRHPLP[0][1] - RRHPLP[0][0]);

            List<double> amp = new List<double>(RRHPLP[1].Count);
            List<double> freq = new List<double>(RRHPLP[1].Count);

            //Writing time and values
            for (int i = 0; i < hilb.Length-1; i++)
            {
                amp.Add(Complex.Abs(hilb[i]));

                if (i < hilb.Length - 1)
                {
                    double phase = hilb[i].Phase;
                    if (phase < 0) phase = Math.PI * 2 + phase;
                    double phase2 = hilb[i + 1].Phase;
                    if (phase2 < 0) phase2 = Math.PI * 2 + phase2;

                    double frequency = Fs / (2 * Math.PI) * (phase2 - phase);
                    freq.Add(frequency);
                }
            }

            hAmp.Add(RRHPLP[0].GetRange(0, RRHPLP[0].Count - 1));
            hFreq.Add(RRHPLP[0].GetRange(0, RRHPLP[0].Count - 1));
            hAmp.Add(amp);
            hFreq.Add(freq);
        }

        private static Complex[] MatlabHilbert(double[] xr)
        {
            var x = (from sample in xr select new Complex(sample, 0)).ToArray();
            Fourier.BluesteinForward(x, FourierOptions.Default);
            var h = new double[x.Length];
            var fftLengthIsOdd = (x.Length & 1) == 1;
            if (fftLengthIsOdd)
            {
                h[0] = 1;
                for (var i = 1; i < xr.Length / 2; i++) h[i] = 2;
            }
            else
            {
                h[0] = 1;
                h[(xr.Length / 2)] = 1;
                for (var i = 1; i < xr.Length / 2; i++) h[i] = 2;
            }
            for (var i = 0; i < x.Length; i++)
            {
                x[i] *= h[i];
            }
            Fourier.BluesteinInverse(x, FourierOptions.Default);


            return x;
        }

        void medianFilter(List<List<double>> hFreq, List<List<double>> hAmp)
        {
            medianFilter(hAmp);
            medianFilter(hFreq);
        }

        #region
        /// <summary>
        /// Function that filters signal using a moving window of 60 points
        /// </summary>
        /// <param name="h_freq"> Signal - Hilbert's frequencies </param>
        /// <param name="h"> Signal - Hilbert's amplitudes </param>
        /// <returns> Filtered amplitudes and frequencies </returns>
        #endregion

        void medianFilter(List<List<double>> h)
        {
            List<double> hAmpFiltered = new List<double>(h[1].Count);
            List<double> hFreqFiltered = new List<double>(h[1].Count);

            int medianFilterWindowLength = 181;
            int filterIndex = 0;
            int sortedWindowIndex = 0;
            double[] medianFilterWindow = new double[medianFilterWindowLength];
            double[] sortedMedianFilterWindow = new double[medianFilterWindowLength];

            for (int i = 0; i < h[1].Count; i++)
            {
                medianFilterWindow[filterIndex] = h[1][i];
                sortedMedianFilterWindow[sortedWindowIndex] = h[1][i];
                filterIndex++;         
                if (i < medianFilterWindowLength)
                {
                    sortedWindowIndex++;
                    continue;                    
                }

                Array.Sort(sortedMedianFilterWindow);

                double median = 0.0;
                if(medianFilterWindowLength % 2 == 0)
                {
                    median = (sortedMedianFilterWindow[medianFilterWindowLength / 2 - 1] + sortedMedianFilterWindow[medianFilterWindowLength / 2]) / 2.0;
                }
                else
                {
                    median = sortedMedianFilterWindow[medianFilterWindowLength/2];
                }
                hAmpFiltered.Add(median);

                if (filterIndex >= medianFilterWindowLength)
                {
                    filterIndex = 0;
                }

                double oldestValue = medianFilterWindow[filterIndex];
                sortedWindowIndex = Array.FindIndex(sortedMedianFilterWindow, x => x == oldestValue);
            }

            h[1] = hAmpFiltered;
            h[0] = h[0].GetRange(medianFilterWindowLength / 2, hAmpFiltered.Count);
        }

        //Normalization of amplitude signal
        void ampNormalization(List<List<double>> hAmp)
        {
            double mean = hAmp[1].Average();

            for(int i = 0 ; i < hAmp[1].Count ; i++)
            {
                hAmp[1][i] = hAmp[1][i] / mean;
            }
        }

        #region
        /// <summary>
        /// Function that detects Apnea(if the frequency goes below 0,06 Hz and the amplitude goes above max amplitude the same time)
        /// </summary>
        /// <param name="hFreq"> Signal - filtered Hilbert's frequencies </param>
        /// <param name="hAmp"> Signal - filtered Hilbert's amplitudes </param>
        /// <returns> The percentage value of sleep apnea in signal (il_Apnea), Hilbert's amplitudes (h_amp) and time periods for which detected apnea (Detected_Apnea) </returns>
        #endregion
        void detectApnea(List<List<double>> hAmp, List<List<double>> hFreq, out List<bool> detected, out List<double> time)
        {
            //Finding the minimum and maximum Hilbert amplitudes
            
            double a, b, min_amp, max_amp, mid, y_amp, y_freq;
            min_amp = double.MaxValue;
            max_amp = 0;

            for (int i = 0; i < hAmp[1].Count(); i++)
            {
                if (hAmp[1][i] > max_amp) max_amp = hAmp[1][i];
                if (hAmp[1][i] < min_amp) min_amp = hAmp[1][i];
            }
            //The minimum Hilbert amplitude threshold (a linear function of the minimum and maximum Hilbert amplitudes):
            a = -0.555; b = 1.3;
            //mid = the midpoint of the minimum and maximum amplitudes
            mid = (max_amp + min_amp) * 0.5;
            y_amp = a + b * (mid + 1) * 0.5;

            //The maximum Hilbert frequency threshold [Hz]:
            y_freq = 0.06;

            //Apnea detection
            double analysisStep = 60; //60 sec for step
            double analysisWindowLength = 5 * analysisStep; //5 min for analysis window

            detected = new List<bool>(hAmp[0].Count);
            time = new List<double>(hAmp[0].Count);

            bool quit = false;
            int ii = 0;
            while(true)
            {
                int j = ii;
                double sumAmp = 0;
                double sumFreq = 0;
                while (hAmp[0][j] - hAmp[0][ii] < analysisWindowLength)
                {
                    if(j == hAmp[0].Count - 1)
                    {
                        quit = true;
                        break;
                    }
                    sumAmp += hAmp[1][j];
                    sumFreq += hFreq[1][j];
                    j++;
                }
                sumAmp -= hAmp[1][j];
                sumFreq -= hFreq[1][j];
                double meanAmp = sumAmp / (j - ii - 1);
                double meanFreq = sumFreq / (j - ii - 1);

                if (meanAmp > y_amp && meanFreq < y_freq)
                {
                    detected.Add(true);
                }
                else
                {
                    detected.Add(false);
                }
                time.Add(hAmp[0][ii]);

                int oldii = ii;
                while(hAmp[0][ii] - hAmp[0][oldii] < analysisStep)
                {
                    if(ii ==  hAmp[0].Count-1)
                    {
                        quit = true;
                        break;
                    }
                    ii++;
                }

                if(quit)
                {
                    break;
                }
            }         
        }

        List<Tuple<int, int>> setResult(List<bool> detected, List<double> time, out double ilApnea)
        {
            //Calculating the percentage of sleep apnea         
            int posCount = 0;
            int negCount = 0;
            List<Tuple<int, int>> detectedApnea = new List<Tuple<int, int>>();
            double start = -1;
            for (int i = 0; i < detected.Count; i++)
            {
                if (detected[i])
                {
                    posCount++;
                    if (start == -1)
                    {
                        start = time[i];
                    }
                    else if (i == detected.Count - 1)
                    {
                        detectedApnea.Add(new Tuple<int, int>((int)start, (int)time[i]));
                    }
                }
                else
                {
                    negCount++;
                    if (start != -1)
                    {
                        detectedApnea.Add(new Tuple<int, int>((int)start, (int)time[i]));
                    }
                }
            }
            ilApnea = ((double)posCount) / (posCount + negCount);

            return detectedApnea;
        }


    }
}

