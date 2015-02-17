﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeHub.Common.Core.Constants;

namespace TradeHubGui.Common.Models
{
    /// <summary>
    /// Generic Provider class used for Market Data Provider or Order Execution Provider
    /// </summary>
    public class Provider : INotifyPropertyChanged
    {
        #region Fields

        private string _providerName;
        private ConnectionStatus _connectionStatus;
        private List<ProviderCredential> _providerCredentials;

        /// <summary>
        /// 
        /// KEY = Symbol
        /// VALUE = <see cref="TickDetails"/>
        /// </summary>
        private Dictionary<string, TickDetails> _tickDetailsMap; 

        #endregion

        #region Constructors

        public Provider()
        {
            _providerCredentials = new List<ProviderCredential>();
            _tickDetailsMap = new Dictionary<string, TickDetails>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Provider name
        /// </summary>
        public string ProviderName
        {
            get { return _providerName; }
            set
            {
                if (_providerName != value)
                {
                    _providerName = value;
                    OnPropertyChanged("ProviderName");
                }
            }
        }

        /// <summary>
        /// Provider connection status
        /// </summary>
        public ConnectionStatus ConnectionStatus
        {
            get { return _connectionStatus; }
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged("ConnectionStatus");
                }
            }
        }

        /// <summary>
        /// List of credentials for provider (i.e. Username, Password, IpAddress etc.)
        /// </summary>
        public List<ProviderCredential> ProviderCredentials
        {
            get { return _providerCredentials; }
            set
            {
                if (_providerCredentials != value)
                {
                    _providerCredentials = value;
                    OnPropertyChanged("Credentials");
                }
            }
        }

        /// <summary>
        /// 
        /// KEY = Symbol
        /// VALUE = <see cref="TickDetails"/>
        /// </summary>
        public Dictionary<string, TickDetails> TickDetailsMap
        {
            get { return _tickDetailsMap; }
            set { _tickDetailsMap = value; }
        }

        #endregion

        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}
