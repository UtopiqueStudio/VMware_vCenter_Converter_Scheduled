using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Web.Services.Protocols;
using System.IO;
using ConverterApi;

namespace VMConverter {

    class Program {

        //Source Physical Machine details
        private String _sourceName;
        private String _sourceUsername;
        private String _sourcePassword;
        private String _sourceOsType;
        private int _sourceAgentPort = 0; /*9089*/
        private String _sourceThumbprint;
        private bool _sourceReboot = false;

        //Converter server details
        private String _converterServerName;
        private String _converterServerUsername;
        private String _converterServerPassword;

        // Jobs & Job Servers
        private Dictionary<int, string> jobServers = new Dictionary<int, string>();
        private Dictionary<int, int> jobNumbers = new Dictionary<int, int>();

        //Target VC Server details
        private String _vcServerName;
        private String _vcThumbprint;
        private String _vcUsername;
        private String _vcPassword;
        private String _vcVMToCreate;

        //Windows Physical to Virtual (P2V) Conversion Job details
        private String _conversionJobName;
        private String _conversionJobDescription;

        //Target VM attributes
        private int _vmvCPUs = 0;
        private int _vmMem = 0;

        private String _propertiesFile;

        private ConverterConnection _converterServer = null;

        static void Main(string[] args) {
            Program program = new Program();
            program.Run(args);
        }

        void Run(string[] args) {

            if (args.Length < 1 || args[0].Equals("/?")) {
                Console.WriteLine("ERROR: Missing command line argument.");
                Console.WriteLine("USAGE: ConverterSamples <propertiesfile>");
                return;
            }

            _propertiesFile = args[0];

            if (!GetInputFromFile()) {
                System.Console.WriteLine("ERROR: Failed to load input file.");
                return;
            }

            _converterServer = new ConverterConnection();
            ConverterService _converterService = new ConverterService();

            if (!_converterServer.Connect("https://" + _converterServerName + "/converter/sdk", _converterServerUsername, _converterServerPassword)) {
                System.Console.WriteLine("ERROR: Failed to Connect to Converter Server.");
                return;
            }

            if (_sourceOsType == "windowsOs" && _sourceAgentPort > 0) {

                ConverterConnection.InstallAgentResult result = _converterServer.InstallAgent(_sourceName, _sourceAgentPort, _sourceThumbprint, _sourceUsername, _sourcePassword, !_sourceReboot);
                
                if (!result._succeeded) {
                    System.Console.WriteLine("ERROR: There was a problem with Agent installation on the physical source.");
                    return;
                }

                // update source thumbprint with the one from agent install
                _sourceThumbprint = result._agentThumbprint;

            }

            foreach (int serverID in jobNumbers.Keys) {

                System.Console.WriteLine("Synchronizing (Server): " + _converterServerName + " / (Job): " + jobNumbers[serverID].ToString());
                System.Console.WriteLine("Updating conversion job parameters...");

                ConverterServerConversionConversionParamsUpdateSpec newParams = new ConverterServerConversionConversionParamsUpdateSpec();
                newParams.synchronizationStartTimeSpecified = true;
                newParams.synchronizationStartTime = DateTime.Now;
                newParams.doFinalizeSpecified = true;
                newParams.doFinalize = false;
                System.Console.WriteLine("Synchronizing job on converter server...");

                try {
                    _converterServer.UpdateJob(jobNumbers[serverID], newParams);
                } catch (Exception ex) {
                    System.Console.WriteLine(ex.Message);
                    System.Console.WriteLine("*** Stack Trace ***");
                    System.Console.WriteLine(ex.StackTrace);
                }
            }  

            /*
            ConverterServerConversionConversionJobSpec jobSpec = BuildConversionJobSpec();
            if (jobSpec == null)
            {
                System.Console.WriteLine("ERROR: Failed to build Conversion Job specification.");
                return;
            }
            ConverterServerConversionConversionJobInfo conversionJobInfo = _converterServer.SubmitJob(jobSpec);
            if (conversionJobInfo == null)
            {
                System.Console.WriteLine("ERROR: Failed to submit P2V Conversion Job.");
                return;
            }
            System.Console.WriteLine("Conversion Job Id = {0} created sucessfully.", conversionJobInfo.key);
            */

        }

        private bool GetInputFromFile() {

            TextReader textReader;
            try {
                textReader = new StreamReader(_propertiesFile);
            } catch (System.IO.FileNotFoundException e) {
                Console.WriteLine("Caught Exception : " + " Name : " + e.GetType().Name + " Message : " + e.Message + " Trace : " + e.StackTrace);
                return false;
            }

            String readLine = textReader.ReadLine();
            String[] keyValuePair;

            do {
                keyValuePair = readLine.Split(new Char[] { '=' });
                switch (keyValuePair[0]) {
                    case "physicalsource.address":
                        _sourceName = keyValuePair[1];
                        break;
                    case "physicalsource.username":
                        _sourceUsername = keyValuePair[1];
                        break;
                    case "physicalsource.password":
                        _sourcePassword = keyValuePair[1];
                        break;
                    case "physicalsource.ostype":
                        _sourceOsType = keyValuePair[1];
                        break;
                    case "physicalsource.agentport":
                        _sourceAgentPort = Int32.Parse(keyValuePair[1]);
                        break;
                    case "physicalsource.thumbprint":
                        _sourceThumbprint = keyValuePair[1];
                        break;
                    case "physicalsource.reboot":
                        _sourceReboot = Boolean.Parse(keyValuePair[1]);
                        break;
                    case "vcserver.address":
                        _vcServerName = keyValuePair[1];
                        break;
                    case "vcserver.thumbprint":
                        _vcThumbprint = keyValuePair[1];
                        break;
                    case "vcserver.username":
                        _vcUsername = keyValuePair[1];
                        break;
                    case "vcserver.password":
                        _vcPassword = keyValuePair[1];
                        break;
                    case "conversion.job.servers":
                        string[] serverList = keyValuePair[1].Split(',');
                        int count = 1;
                        foreach (string server in serverList)
                        {
                            jobServers.Add(count, server);
                            count++;
                        }
                        break;
                    case "conversion.job.numbers":
                        string[] jobList = keyValuePair[1].Split(',');

                        count = 1;

                        foreach (string job in jobList) {
                            jobNumbers.Add(count, Int32.Parse(job));
                            count++;
                        }

                        break;

                    case "conversion.job.name":
                        _conversionJobName = keyValuePair[1];
                        break;
                    case "conversion.job.description":
                        _conversionJobDescription = keyValuePair[1];
                        break;
                    case "conversion.vmtocreate.name":
                        _vcVMToCreate = keyValuePair[1];
                        break;
                    case "conversion.vmtocreate.vcpu":
                        _vmvCPUs = Int32.Parse(keyValuePair[1]);
                        break;
                    case "conversion.vmtocreate.memory":
                        _vmMem = Int32.Parse(keyValuePair[1]);
                        break;
                    case "converterserver.address":
                        _converterServerName = keyValuePair[1];
                        break;
                    case "converterserver.username":
                        _converterServerUsername = keyValuePair[1];
                        break;
                    case "converterserver.password":
                        _converterServerPassword = keyValuePair[1];
                        break;
                }

                readLine = textReader.ReadLine();

            } while (readLine != null);

            _sourceUsername = _sourceUsername.Replace("\\\\", "\\");
            _vcUsername = _vcUsername.Replace("\\\\", "\\");
            _converterServerUsername = _converterServerUsername.Replace("\\\\", "\\");

            textReader.Close();

            return true;
        }

        private ConverterServerConversionConversionJobSpec BuildConversionJobSpec()
        {
            ConverterServerConversionConversionJobSpec jobSpec = new ConverterServerConversionConversionJobSpec();
            jobSpec.name = _conversionJobName;
            jobSpec.description = _conversionJobDescription;
            jobSpec.firstRunSpecified = false;
            jobSpec.startSuspended = false;
            jobSpec.startSuspendedSpecified = true;
            jobSpec.source = BuildLiveSourceSpec();
            jobSpec.conversionParams = BuildConversionParams();
            jobSpec.p2vSourceModificationSpec = new ConverterServerConversionP2VSourceModificationSpec();
            return jobSpec;
        }

        private ConverterComputerSpec BuildLiveSourceSpec()
        {
            ConverterComputerSpec liveSourceSpec = new ConverterComputerSpec();
            liveSourceSpec.location = Common.BuildLiveSourceLocation(_sourceName, _sourceUsername, _sourcePassword, _sourceOsType, _sourceThumbprint);
            return liveSourceSpec;
        }

        private ConverterConversionParams BuildConversionParams()
        {
            ConverterConversionParams conversionParams = new ConverterConversionParams();
            conversionParams.doClone = true;
            conversionParams.doCloneSpecified = true;
            conversionParams.cloningParams = BuildCloningParams();
            conversionParams.doReconfig = true;
            conversionParams.doReconfigSpecified = true;
            conversionParams.reconfigParams = null;
            conversionParams.doInstallTools = false;
            conversionParams.doInstallToolsSpecified = true;
            conversionParams.doCustomize = false;
            conversionParams.doCustomizeSpecified = true;
            conversionParams.customizationParams = null;
            conversionParams.powerOnTargetVM = false;
            conversionParams.powerOnTargetVMSpecified = true;
            conversionParams.removeRestoreCheckpoints = false;
            conversionParams.removeRestoreCheckpointsSpecified = true;
            conversionParams.throttlingParams = null;
            return conversionParams;
        }

        private ConverterCloningParams BuildCloningParams()
        {
            ConverterCloningParams cloningParams = new ConverterCloningParams();
            ConverterTargetVmSpec targetVMSpec = new ConverterTargetVmSpec();
            targetVMSpec.name = _vcVMToCreate;
            targetVMSpec.location = BuildTargetVMLocation();
            targetVMSpec.productVersion = "PRODUCT_MANAGED";
            cloningParams.target = targetVMSpec;
            ConverterStorageParams converterStorageParams = new ConverterStorageParams();
            converterStorageParams.cloningMode = "volumeBasedCloning";
            cloningParams.storageParams = converterStorageParams;
            ConverterBasicHardwareParams hardwareParams = new ConverterBasicHardwareParams();
            if (_vmMem > 0)
            {
                hardwareParams.memoryMB = _vmMem;
                hardwareParams.memoryMBSpecified = true;
            }
            if (_vmvCPUs > 0)
            {
                hardwareParams.numCPUs = _vmvCPUs;
                hardwareParams.numCPUsSpecified = true;
            }
            cloningParams.basicHardwareParams = hardwareParams;
            cloningParams.ovfParams = new ConverterOvfParams();
            return cloningParams;
        }

        private ConverterTargetVmSpecManagedVmLocation BuildTargetVMLocation()
        {
            ConverterTargetVmSpecManagedVmLocation targetVMLocation = new ConverterTargetVmSpecManagedVmLocation();
            ConverterVimConnectionSpecLoginVimCredentials vimCredentials = new ConverterVimConnectionSpecLoginVimCredentials();
            vimCredentials.password = _vcPassword;
            vimCredentials.username = _vcUsername;
            ConverterVimConnectionSpec vimConnectionSpec = new ConverterVimConnectionSpec();
            vimConnectionSpec.hostname = _vcServerName;
            vimConnectionSpec.credentials = vimCredentials;
            vimConnectionSpec.verifyPeer = true;
            vimConnectionSpec.verifyPeerSpecified = true;
            vimConnectionSpec.sslThumbprint = _vcThumbprint;
            targetVMLocation.vimConnect = vimConnectionSpec;
            return targetVMLocation;
        }
    }
}
