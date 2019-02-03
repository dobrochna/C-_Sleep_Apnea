﻿using EKG_Project.IO;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EKG_Project.Modules.Sleep_Apnea
{
    public class Sleep_Apnea_Stats : IModuleStats
    {
        private enum State { START_CHANNEL, CALCULATE, NEXT_CHANNEL, END };
        private bool _aborted;
        private bool _ended;
        private string _analysisName;
        private Sleep_Apnea_Data _data;
        private Basic_Data _basicData;
        private Dictionary<string, Object> _strToObj;
        private Dictionary<string, string> _strToStr;
        private State _currentState;
        private string _currentName;
        private int _currentChannelIndex;
        private double _fs;

        public void Abort()
        {
            _aborted = true;
        }

        public bool Aborted()
        {
            return _aborted;
        }

        public bool Ended()
        {
            return _ended;
        }

        public Dictionary<string, object> GetStats()
        {
            if (_strToObj == null) throw new NullReferenceException();

            return _strToObj;
        }

        public Dictionary<string, string> GetStatsAsString()
        {
            if (_strToStr == null) throw new NullReferenceException();

            return _strToStr;
        }

        public void Init(string analysisName)
        {
            _analysisName = analysisName;
            _ended = false;
            _aborted = false;
            _strToObj = new Dictionary<string, object>();
            _strToStr = new Dictionary<string, string>();
            Sleep_Apnea_Data_Worker worker = new Sleep_Apnea_Data_Worker(analysisName);
            worker.Load();
            _data = worker.Data;
            _currentState = State.START_CHANNEL;
            _currentChannelIndex = 0;
            Basic_Data_Worker basicWorker = new Basic_Data_Worker(analysisName);
            basicWorker.Load();
            _basicData = basicWorker.BasicData;
            _fs = _basicData.Frequency;
        }

        public void ProcessStats()
        {
            switch (_currentState)
            {
                case (State.START_CHANNEL):
                    _currentName = _data.Detected_Apnea[_currentChannelIndex].Item1;
                    _currentState = State.CALCULATE;
                    break;
                case (State.CALCULATE):
                    List<Tuple<int, int>> currentData = _data.Detected_Apnea[_currentChannelIndex].Item2;
                    
                    _strToStr.Add(_currentName + " procent wystąpienia bezdechu: ", _data.il_Apnea[_currentChannelIndex].Item2.ToString());
                    _strToObj.Add(_currentName + " procent wystąpienia bezdechu: ", _data.il_Apnea[_currentChannelIndex].Item2);
                    int i = 1;
                    foreach(var elem in currentData)
                    {
                        double from = (double)elem.Item1 / _fs;
                        double to = (double)elem.Item2 / _fs;
                        _strToStr.Add(_currentName + ", interwał " + i.ToString() + ": ", string.Format("{0} - {1}", from, to));
                        Tuple<double, double> fromTo = new Tuple<double, double>(from, to);
                        _strToObj.Add(_currentName + ", interwał " + i.ToString() + ": ", fromTo);
                        i++;
                    }        
                    
                    _currentState = State.NEXT_CHANNEL;
                    break;
                case (State.NEXT_CHANNEL):
                    _currentChannelIndex++;
                    if (_currentChannelIndex >= _data.Detected_Apnea.Count)
                    {
                        _currentState = State.END;
                    }
                    else _currentState = State.START_CHANNEL;
                    break;
                case (State.END):
                    _ended = true;
                    break;
            }
        }

        public static void Main(String[] args)
        {
            Sleep_Apnea_Stats stats = new Sleep_Apnea_Stats();
            stats.Init("Analysis6");

            while (true)
            {
                if (stats.Ended()) break;
                stats.ProcessStats();
            }

            foreach (var key in stats.GetStatsAsString().Keys)
            {
                Console.WriteLine(key + stats.GetStatsAsString()[key]);
            }
            Console.Read();

        }
    }
}
