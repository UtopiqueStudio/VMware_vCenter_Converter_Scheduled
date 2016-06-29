package SubmitWinP2VJob;

import com.vmware.converter.*;

import java.util.*;
import java.io.*;


public class Program
{
   //Source Physical Machine details
   private String _sourceName;
   private String _sourceUsername;
   private String _sourcePassword;
   private String _sourceOsType;
   private int _sourceAgentPort = 0; /*9089*/
   private String _sourceThumbprint;
   private boolean _sourceReboot = false;

   //Converter server details
   private String _converterServerName;
   private String _converterServerUsername;
   private String _converterServerPassword;

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
   private long _vmMem = 0;

   private String _propertiesFile;

   private ConverterConnection _converterServer = null;

   public static void
   main(String[] args)
   {
      Program program = new Program();
      program.run(args);
   }

   void
   run(String[] args)
   {
      if (args.length < 1 || args[0].equals("/?"))
      {
         System.out.println("ERROR: Missing command line argument.");
         System.out.println("USAGE: ConverterSamples <propertiesfile>");
         return;
      }
      _propertiesFile = args[0];

      if (!getInputFromFile())
      {
         System.out.println("ERROR: Failed to load input file.");
         return;
      }

      System.setProperty("org.apache.axis.components.net.SecureSocketFactory",
                         "org.apache.axis.components.net.SunFakeTrustSocketFactory");

      _converterServer = new ConverterConnection();
      if (!_converterServer.connect("https://" + _converterServerName + "/converter/sdk", _converterServerUsername, _converterServerPassword))
      {
         System.out.println("ERROR: Failed to Connect to Converter Server.");
         return;
      }
      if (_sourceOsType.equals("windowsOs") && (_sourceAgentPort > 0))
      {
         ConverterConnection.InstallAgentResult result =
            _converterServer.InstallAgent(_sourceName,
                                          _sourceAgentPort,
                                          _sourceThumbprint,
                                          _sourceUsername,
                                          _sourcePassword,
                                          !_sourceReboot);
         if (!result.getSucceeded())
         {
            System.out.println("ERROR: There was a problem with Agent installation on the physical source.");
            return;
         }

         // update source thumbprint with the one from agent install
         _sourceThumbprint = result.getAgentThumbprint();
      }
      ConverterServerConversionConversionJobSpec jobSpec = buildConversionJobSpec();
      if (jobSpec == null)
      {
         System.out.println("ERROR: Failed to build Conversion Job specification.");
         return;
      }

      ConverterServerConversionConversionJobInfo conversionJobInfo = _converterServer.submitJob(jobSpec);

      if (conversionJobInfo == null)
      {
         System.out.println("ERROR: Failed to submit P2V Conversion Job.");
         return;
      }

      System.out.println("Conversion Job Id = " + conversionJobInfo.getKey() + " created sucessfully.");
   }

   private boolean getInputFromFile()
   {
      Properties configFile = new Properties();
      try
      {
         configFile.load(new FileInputStream(_propertiesFile));
      }
      catch(IOException e)
      {
         System.out.println(e.getMessage());
         e.printStackTrace();
         return false;
      }
      _sourceName = configFile.getProperty("physicalsource.address");
      _sourceUsername = configFile.getProperty("physicalsource.username");
      _sourcePassword = configFile.getProperty("physicalsource.password");
      _sourceOsType = configFile.getProperty("physicalsource.ostype");
      _sourceAgentPort = Integer.parseInt(configFile.getProperty("physicalsource.agentport"));
      _sourceThumbprint = configFile.getProperty("physicalsource.thumbprint");
      _sourceReboot = Boolean.parseBoolean(configFile.getProperty("physicalsource.reboot"));

      _vcServerName = configFile.getProperty("vcserver.address");
      _vcThumbprint = configFile.getProperty("vcserver.thumbprint");
      _vcUsername = configFile.getProperty("vcserver.username");
      _vcPassword = configFile.getProperty("vcserver.password");

      _conversionJobName = configFile.getProperty("conversion.job.name");
      _conversionJobDescription = configFile.getProperty("conversion.job.description");
      _vcVMToCreate = configFile.getProperty("conversion.vmtocreate.name");
      _vmvCPUs = Integer.parseInt(configFile.getProperty("conversion.vmtocreate.vcpu"));
      _vmMem = Long.parseLong(configFile.getProperty("conversion.vmtocreate.memory"));

      _converterServerName = configFile.getProperty("converterserver.address");
      _converterServerUsername = configFile.getProperty("converterserver.username");
      _converterServerPassword = configFile.getProperty("converterserver.password");

      return true;
   }

   private ConverterServerConversionConversionJobSpec
   buildConversionJobSpec()
   {
      ConverterServerConversionConversionJobSpec jobSpec =
                              new ConverterServerConversionConversionJobSpec();
      jobSpec.setName(_conversionJobName);
      jobSpec.setDescription(_conversionJobDescription);
      jobSpec.setStartSuspended(false);
      jobSpec.setSource(buildLiveSourceSpec());
      jobSpec.setConversionParams(buildConversionParams());
      jobSpec.setP2VSourceModificationSpec(new ConverterServerConversionP2VSourceModificationSpec());

      return jobSpec;
   }

   private ConverterComputerSpec
   buildLiveSourceSpec()
   {
      ConverterComputerSpec liveSourceSpec = new ConverterComputerSpec();
      liveSourceSpec.setLocation(
         Common.buildLiveSourceLocation(_sourceName, _sourceUsername, _sourcePassword, _sourceOsType, _sourceThumbprint));

      return liveSourceSpec;
   }

   private ConverterConversionParams
   buildConversionParams()
   {
      ConverterConversionParams conversionParams = new ConverterConversionParams();
      conversionParams.setDoClone(true);
      conversionParams.setCloningParams(buildCloningParams());
      conversionParams.setDoReconfig(true);
      conversionParams.setReconfigParams(null);
      conversionParams.setDoInstallTools(false);
      conversionParams.setDoCustomize(false);
      conversionParams.setCustomizationParams(null);
      conversionParams.setPowerOnTargetVM(false);
      conversionParams.setRemoveRestoreCheckpoints(false);
      conversionParams.setThrottlingParams(null);

      return conversionParams;
   }

   private ConverterCloningParams
   buildCloningParams()
   {
      ConverterCloningParams cloningParams = new ConverterCloningParams();

      ConverterTargetVmSpec targetVMSpec = new ConverterTargetVmSpec();
      targetVMSpec.setName(_vcVMToCreate);
      targetVMSpec.setLocation(buildTargetVMLocation());
      targetVMSpec.setProductVersion("PRODUCT_MANAGED");

      cloningParams.setTarget(targetVMSpec);
      ConverterStorageParams converterStorageParams = new ConverterStorageParams();
      converterStorageParams.setCloningMode("volumeBasedCloning");
      cloningParams.setStorageParams(converterStorageParams);
      ConverterBasicHardwareParams hardwareParams = new ConverterBasicHardwareParams();
      if(_vmMem > 0)
      {
         hardwareParams.setMemoryMB(_vmMem);
      }
      if(_vmvCPUs > 0)
      {
         hardwareParams.setNumCPUs(_vmvCPUs);
      }
      cloningParams.setBasicHardwareParams(hardwareParams);
      cloningParams.setOvfParams(new ConverterOvfParams());

      return cloningParams;
   }

   private ConverterTargetVmSpecManagedVmLocation
   buildTargetVMLocation()
   {
      ConverterTargetVmSpecManagedVmLocation targetVMLocation =
                                 new ConverterTargetVmSpecManagedVmLocation();

      ConverterVimConnectionSpecLoginVimCredentials vimCredentials =
                                 new ConverterVimConnectionSpecLoginVimCredentials();
      vimCredentials.setPassword(_vcPassword);
      vimCredentials.setUsername(_vcUsername);

      ConverterVimConnectionSpec vimConnectionSpec = new ConverterVimConnectionSpec();
      vimConnectionSpec.setHostname(_vcServerName);
      vimConnectionSpec.setCredentials(vimCredentials);
      vimConnectionSpec.setVerifyPeer(true);
      vimConnectionSpec.setSslThumbprint(_vcThumbprint);

      targetVMLocation.setVimConnect(vimConnectionSpec);

      return targetVMLocation;
   }
}
