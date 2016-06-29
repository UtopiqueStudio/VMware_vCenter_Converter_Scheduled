package SubmitWinP2VJob;

import com.vmware.converter.*;

import java.io.IOException;
import java.net.URL;
import java.util.*;

import org.apache.axis.MessageContext;
import org.apache.axis.client.Stub;
import org.apache.axis.transport.http.HTTPConstants;

public class ConverterConnection
{
   private ConverterServiceLocator _converterServiceLocator = null;
   private ConverterPortType _converterService = null;
   private ConverterServerContent _converterServerContent = null;
   private ConverterUserSession _userSession = null;
   private int _waitSeconds = 180;
   private int _totalAttempts = 5;

   public static class InstallAgentResult
   {
      private boolean _succeeded;
      private String  _agentThumbprint;

      public InstallAgentResult(boolean succeeded, String thumbprint)
      {
         _succeeded = succeeded;
         _agentThumbprint = thumbprint;
      }

      public boolean
      getSucceeded()
      {
         return _succeeded;
      }

      public String
      getAgentThumbprint()
      {
         return _agentThumbprint;
      }
   }

   public boolean
   connect(String serverUrl, String username, String password)
   {
      try
      {
         ManagedObjectReference morServiceInstance = new ManagedObjectReference();
         morServiceInstance.setType("ConverterServiceInstance");
         morServiceInstance.set_value("ServiceInstance");

         _converterServiceLocator = new ConverterServiceLocator();
         _converterServiceLocator.setMaintainSession(true);

         _converterService = _converterServiceLocator.getConverterPort(new URL(serverUrl));
         ((Stub)_converterService).setTimeout(120000);

         Hashtable<String, String> properties = new Hashtable<String, String>();
         properties.put(HTTPConstants.HEADER_TRANSFER_ENCODING_CHUNKED, "false");

         ((Stub)_converterService)._setProperty(HTTPConstants.REQUEST_HEADERS, properties);
         ((Stub)_converterService)._setProperty(MessageContext.HTTP_TRANSPORT_VERSION,
                                                HTTPConstants.HEADER_PROTOCOL_V11);
         ((Stub)_converterService)._setProperty(Stub.SESSION_MAINTAIN_PROPERTY,
                                                Boolean.TRUE);

         _converterServerContent = (ConverterServerContent)_converterService.converterRetrieveServiceContent(morServiceInstance);

         if (_converterServerContent.getSessionManager() == null)
         {
            System.out.println("ERROR: Session Manager is null");
            return false;
         }

         _userSession = _converterService.converterLogin(_converterServerContent.getSessionManager(),
                                                         username, password, null);
         if (_userSession == null)
         {
            return false;
         }

         return true;
      }
      catch(Exception e)
      {
         System.out.println(e.getMessage());
         e.printStackTrace();

         return false;
      }
   }

   public ConverterServerConversionConversionJobInfo
   submitJob(ConverterServerConversionConversionJobSpec jobSpec)
   {
      ConverterServerConversionConversionJobInfo result = null;
      boolean retry;

      do
      {
         retry = false;

         try
         {
            result = _converterService.converterServerConversionManagerCreateJob(
                                                    _converterServerContent.getConversionManager(),
                                                    jobSpec,
                                                    null);
         }
         catch(InvalidArgument e)
         {
            ConverterSSLVerificationFault sslFault = null;
            if (e.getFaultCause() != null)
            {
               sslFault = (ConverterSSLVerificationFault) e.getFaultCause().getFault();
            }
            if (sslFault != null)
            {
               // source machine spec
               ConverterComputerSpecLiveComputerLocation liveSourceLocation =
                  (ConverterComputerSpecLiveComputerLocation) jobSpec.getSource().getLocation();

               // destination connection spec
               ConverterVimConnectionSpec vimConnectionSpec =
                  ((ConverterTargetVmSpecManagedVmLocation)
                     jobSpec.getConversionParams().getCloningParams().getTarget().getLocation()).getVimConnect();

               if (liveSourceLocation != null &&
                     (liveSourceLocation.getSslThumbprint() == null ||
                        liveSourceLocation.getSslThumbprint().length() == 0))
               {
                  if (readYesNo("\nSource machine certificate validation failed.\n" +
                                "Source machine: " + liveSourceLocation.getHostname() + "\n" +
                                "Certificate thumprint: " + sslFault.getThumbprint() + "\n" +
                                "Proceed with connecting to the source machine?", false))
                  {
                     liveSourceLocation.setSslThumbprint(sslFault.getThumbprint());
                     retry = true;
                  }
               }
               else if (vimConnectionSpec != null &&
                             (vimConnectionSpec.getSslThumbprint() == null ||
                                vimConnectionSpec.getSslThumbprint().length() == 0))
               {
                  if (readYesNo("\nDestination vCenter/ESX server certificate validation failed.\n" +
                                "Destination vCenter/ESX server: " + vimConnectionSpec.getHostname() + "\n" +
                                "Certificate thumprint: " + sslFault.getThumbprint() + "\n" +
                                "Proceed with connecting to the destination server?", false))
                  {
                     vimConnectionSpec.setSslThumbprint(sslFault.getThumbprint());
                     retry = true;
                  }
               }
            }
            if (!retry)
            {
               System.out.println(e.getMessage());
               e.printStackTrace();
            }
         }
         catch(Exception e)
         {
            System.out.println(e.getMessage());
            e.printStackTrace();
         }
      }
      while (retry);

      return result;
   }

   public InstallAgentResult
   InstallAgent(String sourceName, int sourceAgentPort, String sourceThumbprint,
                String sourceUsername, String sourcePassword, boolean sourcePostponeReboot)
   {
      // Check for Agent existence.
      ConverterComputerSpecLiveComputerLocation liveComputerLocation =
         Common.buildLiveSourceLocation(sourceName, sourceUsername, sourcePassword, "windowsOs", sourceThumbprint);

      ConverterComputerSpec computerSpec = new ConverterComputerSpec();
      computerSpec.setLocation(liveComputerLocation);
      boolean availabilityValidated = false;

      boolean retry;
      do
      {
         retry = false;
         try
         {
            _converterService.converterValidateAgentAvailability(_converterServerContent.getQueryManager(), computerSpec);
            availabilityValidated = true;
         }
         catch(ConverterSSLVerificationFault e)
         {
            if (liveComputerLocation.getSslThumbprint() == null ||
                  liveComputerLocation.getSslThumbprint().length() == 0)
            {
               if (readYesNo("\nSource machine certificate validation failed.\n" +
                             "Source machine: " + liveComputerLocation.getHostname() + "\n" +
                             "Certificate thumprint: " + e.getThumbprint() + "\n" +
                             "Proceed with connecting to the source machine?", false))
               {
                  liveComputerLocation.setSslThumbprint(e.getThumbprint());
                  retry = true;
               }
            }
            if (!retry)
            {
               System.out.println(e.getMessage());
               e.printStackTrace();
               return new InstallAgentResult(false, null);
            }
         }
         catch(Exception e)
         {
            System.out.println("Agent isn't installed on target machine " + sourceName + " '" + e.getMessage() + "'. Try to install agent...");
         }
      }
      while (retry);

      if (availabilityValidated)
      {
         return new InstallAgentResult(true, liveComputerLocation.getSslThumbprint());
      }

      try
      {
         ConverterAgentManagerAgentDeploymentResult result =
                   _converterService.converterInstallAgent(_converterServerContent.getAgentManager(),
                                                           sourceName, sourceAgentPort,
                                                           sourceUsername, sourcePassword,
                                                           sourcePostponeReboot);
         if (result.getStatus() == ConverterAgentManagerDeploymentStatus.completed)
         {
            return new InstallAgentResult(true, result.getSslThumbprint());
         }
         else if (result.getStatus() == ConverterAgentManagerDeploymentStatus.rebootRequired &&
                  sourcePostponeReboot == true )
         {
            System.out.println("A reboot of the physical source " + sourceName + " is required for " +
                               "the agent installation to succeed. Please try the P2V after rebooting the source");
            return new InstallAgentResult(false, result.getSslThumbprint());
         }
         else if (result.getStatus() == ConverterAgentManagerDeploymentStatus.rebootRequired &&
                  sourcePostponeReboot == false)
         {
            _converterService.converterRebootMachine(_converterServerContent.getAgentManager(), sourceName,
                                                     sourceUsername, sourcePassword);
           System.out.println("A reboot of the physical source " + sourceName + " has been initiated.");
         }

         // Update source computer spec thumbprint with the one returned after agent
         // deployment, if any
         liveComputerLocation.setSslThumbprint(result.getSslThumbprint());

         // If a reboot of the Physical source has been initiated.
         for(int attempt = 0; attempt < _totalAttempts; attempt++)
         {
            try
            {
               _converterService.converterValidateAgentAvailability(_converterServerContent.getQueryManager(), computerSpec);
               availabilityValidated = true;
               break;
            }
            catch (Exception e)
            {
               System.out.println("Caught Exception : " + e.getMessage() + " Trying again...");
               Thread.sleep(_waitSeconds * 1000);
            }
         }
         return new InstallAgentResult(availabilityValidated, result.getSslThumbprint());

      }
      catch (Exception e)
      {
         System.out.println(e.getMessage());
         e.printStackTrace();
      }
      return new InstallAgentResult(false, null);
   }

   private boolean
   readYesNo(String prompt, boolean defaultValue)
   {
      System.out.println(prompt);
      boolean result = defaultValue;
      boolean ready = false;

      do
      {
         System.out.print("(Enter [y]es, [n]o or [c]ancel): > ");

         char ch;
         StringBuffer lineBuffer = new StringBuffer();
         do
         {
            try
            {
               ch = (char) System.in.read();
               if (ch != '\r' && ch != '\n')
               {
                  lineBuffer.append(ch);
               }
            }
            catch (IOException e)
            {
               // something went wrong
               ready = true;
               break;
            }
         }
         while (ch != '\n');

         String line = new String(lineBuffer).trim();
         if (line.compareToIgnoreCase("y") == 0 ||
             line.compareToIgnoreCase("yes") == 0)
         {
            result = true;
            ready = true;
         }
         else if (line.compareToIgnoreCase("n") == 0||
                    line.compareToIgnoreCase("no") == 0)
         {
            result = false;
            ready = true;
         }
         else if (line.compareToIgnoreCase("c") == 0||
                    line.compareToIgnoreCase("cancel") == 0)
         {
            // use default answer
            ready = true;
         }
      }
      while (!ready);

      return result;
   }
}
