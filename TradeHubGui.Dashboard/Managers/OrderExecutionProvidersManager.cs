﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using TraceSourceLogger;
using TradeHubGui.Common.Infrastructure;
using TradeHubGui.Common.Models;

namespace TradeHubGui.Dashboard.Managers
{
    /// <summary>
    /// Handles Order Execution Provider's related Admin functionality
    /// </summary>
    internal class OrderExecutionProvidersManager
    {
        private Type _type = typeof(OrderExecutionProvidersManager);

        /// <summary>
        /// Directory path at which Order Execution Provider's files are located
        /// </summary>
        private readonly string _orderExecutionProvidersRootFolderPath;

        /// <summary>
        /// Directory at which the Order Execution Provider's config files are located
        /// </summary>
        private readonly string _orderExecutionProvidersConfigFolderPath;

        /// <summary>
        /// File name which holds the name of all available order execution providers
        /// </summary>
        private readonly string _orderExecutionProvidersFileName;

        /// <summary>
        /// Default Constructor
        /// </summary>
        public OrderExecutionProvidersManager()
        {
            // Used to Testing
            _orderExecutionProvidersRootFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\TradeHub\\OrderExecutionProviders\\";
            _orderExecutionProvidersConfigFolderPath = _orderExecutionProvidersRootFolderPath;

            //// Used for Live Execution
            //_orderExecutionProvidersRootFolderPath = Path.GetFullPath(@"~\..\..\Order Execution Engine\");
            //_orderExecutionProvidersConfigFolderPath = _orderExecutionProvidersRootFolderPath + @"Config\";

            _orderExecutionProvidersFileName = "AvailableOEProviders.xml";
        }

        /// <summary>
        /// Returns a list of available market data providers
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, List<ProviderCredential>> GetAvailableProviders()
        {
            // File Saftey Check
            if (!File.Exists(_orderExecutionProvidersConfigFolderPath + _orderExecutionProvidersFileName)) 
                return null;

            // Will hold credential information against each availale provider
            IDictionary<string, List<ProviderCredential>> availableProviders = new Dictionary<string, List<ProviderCredential>>();

            // XML Document to read file
            var availableProvidersDocument = new XmlDocument();

            // Read file to get available provider's names.
            availableProvidersDocument.Load(_orderExecutionProvidersConfigFolderPath + _orderExecutionProvidersFileName);

            // Read the all Node value
            XmlNodeList providersInfo = availableProvidersDocument.SelectNodes(xpath: "Providers/*");

            if (providersInfo != null)
            {
                // Extract individual attribute value
                foreach (XmlNode node in providersInfo)
                {
                    // Create file name from which to read Provider Credentials
                    string credentialsFileName = node.Name + @"OrderParams.xml";

                    // XML Document to read provider specific xml file
                    var availableCredentialsDoc = new XmlDocument();

                    // Holds extracted credentials from the xml file
                    var providerCredentialList = new List<ProviderCredential>();

                    if (File.Exists(_orderExecutionProvidersConfigFolderPath + credentialsFileName))
                    {
                        // Read configuration file
                        availableCredentialsDoc.Load(_orderExecutionProvidersConfigFolderPath + credentialsFileName);

                        // Read all the parametes defined in the configuration file
                        XmlNodeList configNodes = availableCredentialsDoc.SelectNodes(xpath: node.Name + "/*");
                        if (configNodes != null)
                        {
                            // Extract individual attribute value
                            foreach (XmlNode innerNode in configNodes)
                            {
                                ProviderCredential providerCredential = new ProviderCredential();

                                providerCredential.CredentialName = innerNode.Name;
                                providerCredential.CredentialValue = innerNode.InnerText;

                                // Add to Credentials list
                                providerCredentialList.Add(providerCredential);
                            }
                        }
                    }
                    // Add all details to providers info map
                    availableProviders.Add(node.Name, providerCredentialList);
                }
            }

            return availableProviders;
        }

        /// <summary>
        /// Edits given Order Execution provider credentails with the new values
        /// </summary>
        /// <param name="provider">Contains provider details</param>
        public bool EditProviderCredentials(Provider provider)
        {
            try
            {
                bool valueSaved = false;

                // Create file path
                string filePath = _orderExecutionProvidersConfigFolderPath + provider.ProviderName + @"OrderParams.xml";

                // Create XML Document Object to read credentials file
                XmlDocument document = new XmlDocument();

                // Load credentials file
                document.Load(filePath);

                XmlNode root = document.DocumentElement;

                // Travers all credential values
                foreach (ProviderCredential providerCredential in provider.ProviderCredentials)
                {
                    XmlNode xmlNode = root.SelectSingleNode("descendant::" + providerCredential.CredentialName);

                    if (xmlNode != null)
                    {
                        xmlNode.InnerText = providerCredential.CredentialValue;
                        valueSaved = true;
                    }
                }

                // Save document
                document.Save(filePath);

                return valueSaved;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "EditProviderCredentials");
                return false;
            }
        }

        /// <summary>
        /// Adds given provider to Order Execution Engine - Server
        /// </summary>
        /// <param name="connectorPath">Complete path for connectors library</param>
        /// <param name="providerName">Name to be used for the given connector</param>
        public Tuple<bool, string> AddProvider(string connectorPath, string providerName)
        {
            // Get root directory path
            string rootDirectory = Path.GetDirectoryName(connectorPath);
            // Get Config folder path
            string configPath = rootDirectory + @"\Config";
            // Get Spring file path
            string springFileName = providerName + "SpringConfig.xml";
            string springFilePath = configPath + @"\" + springFileName;

            if (!VerifySpringConfigFileName(springFilePath))
                return new Tuple<bool, string>(false, "Expected Spring Configuration file not found.");

            if (!CopyProviderLibraries(connectorPath))
                return new Tuple<bool, string>(false, "Given files were not copied to the Server location.");

            if (!ModifyServerSpringParameters(springFileName))
                return new Tuple<bool, string>(false, "Spring configuration was not modified.");

            if (!AddProviderName(providerName))
                return new Tuple<bool, string>(false, "Not able to add new Provider name to Server.");

            return new Tuple<bool, string>(true, "Provider is sucessfully added to the Server.");
        }

        /// <summary>
        /// Removes given provider from the Order Execution Engine - Server
        /// </summary>
        /// <param name="provider">Contains complete provider details</param>
        /// <returns></returns>
        public Tuple<bool, string> RemoveProvider(Provider provider)
        {
            if (!RemoveProviderName(provider.ProviderName))
                return new Tuple<bool, string>(false, "Not able to remove Provider name from Server.");

            return new Tuple<bool, string>(true, "Provider is sucessfully removed from the Server.");
        }

        /// <summary>
        /// Verifies if the valid spring config file name is provided for the given connector
        /// </summary>
        /// <param name="springFile">Complete path for Spring configuration file</param>
        /// <returns></returns>
        private bool VerifySpringConfigFileName(string springFile)
        {
            try
            {
                return File.Exists(springFile);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "VerifySpringConfigFileName");
                return false;
            }
        }

        /// <summary>
        /// Copies given provider and its dependencies to Order Execution Engine - Server
        /// </summary>
        /// <param name="path">Directory path for Provider</param>
        /// <returns></returns>
        private bool CopyProviderLibraries(string path)
        {
            try
            {
                // Get all files information in the given directory
                string[] files = Directory.GetFiles(Path.GetDirectoryName(path), "*", SearchOption.AllDirectories);

                // Copy individual Files
                foreach (string file in files)
                {
                    if (File.Exists(file)) continue;

                    File.Copy(file, _orderExecutionProvidersRootFolderPath + Path.GetFileName(file), false);
                }

                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "CopyProviderLibraries");
                return false;
            }
        }

        /// <summary>
        /// Modifies Server Spring parameters by adding given providers spring object in the spring config
        /// </summary>
        /// <param name="springFileName"></param>
        /// <returns></returns>
        private bool ModifyServerSpringParameters(string springFileName)
        {
            try
            {
                string appConfigName = "TradeHub.OrderExecutionEngine.Server.WindowsService.exe.config";
                string appConfigPath = _orderExecutionProvidersRootFolderPath + appConfigName;

                // Modify configuration file
                return XmlFileManager.ModifyAppConfigForSpringObject(appConfigPath, @"~/Config/" + springFileName);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "ModifyServerSpringParameters");
                return false;
            }
        }

        /// <summary>
        /// Adds given provider name to Order Execution Engine - Server
        /// </summary>
        /// <param name="providerName"></param>
        /// <returns></returns>
        private bool AddProviderName(string providerName)
        {
            try
            {
                string path = _orderExecutionProvidersConfigFolderPath + _orderExecutionProvidersFileName;

                return XmlFileManager.AddChildNode(path, providerName);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "AddProviderName");
                return false;
            }
        }

        /// <summary>
        /// Removes given provider name from Order Execution Engine - Server
        /// </summary>
        /// <param name="providerName"></param>
        /// <returns></returns>
        private bool RemoveProviderName(string providerName)
        {
            try
            {
                string path = _orderExecutionProvidersConfigFolderPath + _orderExecutionProvidersFileName;

                return XmlFileManager.RemoveChildNode(path, providerName);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "AddProviderName");
                return false;
            }
        }
    }
}
