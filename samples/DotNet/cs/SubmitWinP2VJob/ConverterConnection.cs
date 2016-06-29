using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Web.Services.Protocols;
using System.Threading;
using System.Text.RegularExpressions;
using ConverterApi;

namespace VMConverter {

    class ConverterConnection {

        private ConverterService _converterService = null;
        private ConverterServerContent _converterServerContent = null;
        private ConverterUserSession _userSession = null;
        private int _waitSeconds = 180;
        private int _totalAttempts = 5;

        public class InstallAgentResult{

            public bool _succeeded;
            public String _agentThumbprint;

            public InstallAgentResult(bool succeeded, String agentThumbprint) {
                _succeeded = succeeded;
                _agentThumbprint = agentThumbprint;
            }
        }

        public ConverterConversionParams UpdateJob(string jobId, ConverterServerConversionConversionParamsUpdateSpec updateSpec) {
            ManagedObjectReference job = new ManagedObjectReference();
            job.type = "ConverterServerConversionConversionJob";
            job.Value = "job-" + jobId.ToString();
            ConverterConversionParams result = null;

            try {
                result = _converterService.ConverterServerConversionJobUpdateConversionParams(job, updateSpec);
            } catch (Exception e) {
                Console.WriteLine("Caught Exception : \n" +
                                 " Name : " + e.GetType().Name + "\n" +
                                 " Message : " + e.Message + "\n" +
                                 " Trace : " + e.StackTrace);
            }
            return result;
        }  

        public bool Connect(string serverUrl, string username, string password) {

            try {
                ManagedObjectReference morServiceInstance = new ManagedObjectReference();
                morServiceInstance.type = "ConverterServiceInstance";
                morServiceInstance.Value = "ServiceInstance";

                _converterService = new ConverterService();
                _converterService.Url = serverUrl;
                _converterService.CookieContainer = new System.Net.CookieContainer();
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(RemoteCertificateValidation);
                _converterServerContent = (ConverterServerContent)_converterService.ConverterRetrieveServiceContent(morServiceInstance);

                if (_converterServerContent.sessionManager == null) {
                    Console.WriteLine("ERROR: Session Manager is null");
                    return false;
                }

                _userSession = _converterService.ConverterLogin(_converterServerContent.sessionManager, username, password, null);

                if (_userSession == null)
                    return false;
                else
                    return true;

            } catch (SoapException se) {
                Console.WriteLine("Caught SoapException - " +
                                  " Actor : " + se.Actor +
                                  " Code : " + se.Code +
                                  " Detail XML : " + se.Detail.OuterXml);
            } catch (Exception e) {
                Console.WriteLine("Caught Exception : " +
                                  " Name : " + e.GetType().Name +
                                  " Message : " + e.Message +
                                  " Trace : " + e.StackTrace);
            }

            return false;
        }

      // Set certificate policy to allow all certificates
      private static bool RemoteCertificateValidation(Object sender,
                                                      System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                                                      System.Security.Cryptography.X509Certificates.X509Chain chain,
                                                      System.Net.Security.SslPolicyErrors sslPolicyErrors) {
         //Handle bad certificates here
         return true;
      }

      public ConverterServerConversionConversionJobInfo SubmitJob(ConverterServerConversionConversionJobSpec jobSpec) {
         ConverterServerConversionConversionJobInfo result = null;
         bool retry;

         do {
            retry = false;

            try {
               result = _converterService.ConverterServerConversionManagerCreateJob(
                                                   _converterServerContent.conversionManager,
                                                   jobSpec,
                                                   null);
            } catch (SoapException e) {
               String remoteThumbprint = ExtractCertificateThumbprintFromFaultMessage(e.Detail.InnerText);
               if (remoteThumbprint != null)
               {
                  // source machine spec
                  ConverterComputerSpecLiveComputerLocation liveSourceLocation =
                     (ConverterComputerSpecLiveComputerLocation) jobSpec.source.location;

                  // destination connection spec
                  ConverterVimConnectionSpec vimConnectionSpec =
                     ((ConverterTargetVmSpecManagedVmLocation)
                        jobSpec.conversionParams.cloningParams.target.location).vimConnect;

                  if (liveSourceLocation != null &&
                        (liveSourceLocation.sslThumbprint == null ||
                           liveSourceLocation.sslThumbprint.Length == 0))
                  {
                     if (ReadYesNo("\nSource machine certificate validation failed.\n" +
                                   "Source machine: " + liveSourceLocation.hostname + "\n" +
                                   "Certificate thumprint: " + remoteThumbprint + "\n" +
                                   "Proceed with connecting to the source machine?", false))
                     {
                        liveSourceLocation.sslThumbprint = remoteThumbprint;
                        retry = true;
                     }
                  }
                  else if (vimConnectionSpec != null &&
                              (vimConnectionSpec.sslThumbprint == null ||
                                 vimConnectionSpec.sslThumbprint.Length == 0))
                  {
                     if (ReadYesNo("\nDestination vCenter/ESX server certificate validation failed.\n" +
                                   "Destination vCenter/ESX server: " + vimConnectionSpec.hostname + "\n" +
                                   "Certificate thumprint: " + remoteThumbprint + "\n" +
                                   "Proceed with connecting to the destination server?", false))
                     {
                        vimConnectionSpec.sslThumbprint = remoteThumbprint;
                        retry = true;
                     }
                  }
               }
               if (!retry)
               {
                  Console.WriteLine("Caught SoapException - \n" +
                                    " Actor : " + e.Actor + "\n" +
                                    " Code : " + e.Code + "\n" +
                                    " Detail XML : " + e.Detail.OuterXml);
               }
            }
            catch (Exception e)
            {
               Console.WriteLine("Caught Exception : \n" +
                                 " Name : " + e.GetType().Name + "\n" +
                                 " Message : " + e.Message + "\n" +
                                 " Trace : " + e.StackTrace);
            }
         }
         while (retry);

         return result;
      }

      public InstallAgentResult
      InstallAgent(String sourceName, int sourceAgentPort, String sourceThumbprint,
                   String sourceUsername, String sourcePassword, bool sourcePostponeReboot)
      {
         // Check for Agent existence.
         ConverterComputerSpecLiveComputerLocation liveComputerLocation =
            Common.BuildLiveSourceLocation(sourceName, sourceUsername, sourcePassword, "windowsOs", sourceThumbprint);

         ConverterComputerSpec computerSpec = new ConverterComputerSpec();
         computerSpec.location = liveComputerLocation;
         bool availabilityValidated = false;

         bool retry;
         do
         {
            retry = false;
            try
            {
               _converterService.ConverterValidateAgentAvailability(_converterServerContent.queryManager, computerSpec);
               availabilityValidated = true;
            }
            catch (SoapException e)
            {
               String remoteThumbprint = ExtractCertificateThumbprintFromFaultMessage(e.Message);
               if (remoteThumbprint != null)
               {
                  if (liveComputerLocation.sslThumbprint == null ||
                        liveComputerLocation.sslThumbprint.Length == 0)
                  {
                     if (ReadYesNo("\nSource machine certificate validation failed.\n" +
                                   "Source machine: " + liveComputerLocation.hostname + "\n" +
                                   "Certificate thumprint: " + remoteThumbprint + "\n" +
                                   "Proceed with connecting to the source machine?", false))
                     {
                        liveComputerLocation.sslThumbprint = remoteThumbprint;
                        retry = true;
                     }
                  }
                  if (!retry)
                  {
                     Console.WriteLine("Caught SoapException - \n" +
                                       " Actor : " + e.Actor + "\n" +
                                       " Code : " + e.Code + "\n" +
                                       " Detail XML : " + e.Detail.OuterXml);
                     return new InstallAgentResult(false, null);
                  }
               }
               else
               {
                  Console.WriteLine("Agent isn't installed on source machine {0} '" + e.Message + "'. Try to install agent...", sourceName);
               }
            }
            catch (Exception e)
            {
               Console.WriteLine("Agent isn't installed on source machine {0} '" + e.Message + "'. Try to install agent...", sourceName);
            }
         }
         while (retry);

         if (availabilityValidated)
         {
            return new InstallAgentResult(true, liveComputerLocation.sslThumbprint);
         }

         try
         {
            ConverterAgentManagerAgentDeploymentResult result =
                      _converterService.ConverterInstallAgent(_converterServerContent.agentManager,
                                                              sourceName, sourceAgentPort, true,
                                                              sourceUsername, sourcePassword,
                                                              sourcePostponeReboot, true);
            if (result.status == ConverterAgentManagerDeploymentStatus.completed)
            {
               return new InstallAgentResult(true, result.sslThumbprint);
            }
            else if (result.status == ConverterAgentManagerDeploymentStatus.rebootRequired &&
                     sourcePostponeReboot == true )
            {
               Console.WriteLine("A reboot of the physical source {0} is required for the agent installation" +
                                 " to succeed. Please try the P2V after rebooting the source", sourceName);
               return new InstallAgentResult(false, result.sslThumbprint);
            }
            else if (result.status == ConverterAgentManagerDeploymentStatus.rebootRequired &&
                     sourcePostponeReboot == false)
            {
               _converterService.ConverterRebootMachine(_converterServerContent.agentManager, sourceName,
                                                        sourceUsername, sourcePassword);
              Console.WriteLine("A reboot of the physical source {0} has been initiated.", sourceName);
            }

            // Update source computer spec thumbprint with the one returned after agent
            // deployment, if any
            liveComputerLocation.sslThumbprint = result.sslThumbprint;

            // If a reboot of the Physical source has been initiated.
            for(int attempt = 0; attempt < _totalAttempts; attempt++)
            {
               try
               {
                  _converterService.ConverterValidateAgentAvailability(_converterServerContent.queryManager, computerSpec);
                  availabilityValidated = true;
                  break;
               }
               catch (Exception e)
               {
                  Console.WriteLine("Caught Exception : " + e.Message + " Trying again...");
                  System.Threading.Thread.Sleep(_waitSeconds * 1000);
               }
            }
            return new InstallAgentResult(availabilityValidated, result.sslThumbprint);

         }
         catch (SoapException se)
         {
            Console.WriteLine("Caught SoapException - \n" +
                              " Actor : " + se.Actor + "\n" +
                              " Code : " + se.Code + "\n" +
                              " Detail XML : " + se.Detail.OuterXml);
         }
         catch (Exception e)
         {
            Console.WriteLine("Caught Exception : \n" +
                              " Name : " + e.GetType().Name + "\n" +
                              " Message : " + e.Message + "\n" +
                              " Trace : " + e.StackTrace);
         }
         return new InstallAgentResult(false, null);
      }

      private String
      ExtractCertificateThumbprintFromFaultMessage(String message)
      {
         // Thumbprints look like CC:49:57:9A:82:30:8F:3C:AA:3D:01:4C:FA:F6:50:FF:16:1F:C9:65
         const String THUMBPRINT_RE = @"(([0-9a-fA-F]{2}:){19}[0-9a-fA-F]{2})";

         Match match = Regex.Match(message, THUMBPRINT_RE, RegexOptions.None);
         if (match.Success)
         {
            return match.Value.Trim();
         }
         else
         {
            return null;
         }
      }

      private bool
      ReadYesNo(String prompt, bool defaultValue)
      {
         Console.WriteLine(prompt);
         bool result = defaultValue;
         bool ready = false;

         do
         {
            Console.Write("(Enter [y]es, [n]o or [c]ancel): > ");

            String line;
            try
            {
               line = System.Console.ReadLine();
            }
            catch (IOException)
            {
               // something went wrong
               break;
            }

            line = line.Trim().ToLower();
            if (line.CompareTo("y") == 0 ||
                  line.CompareTo("yes") == 0)
            {
               result = true;
               ready = true;
            }
            else if (line.CompareTo("n") == 0 ||
                       line.CompareTo("no") == 0)
            {
               result = false;
               ready = true;
            }
            else if (line.CompareTo("c") == 0 ||
                        line.CompareTo("cancel") == 0)
            {
               // use default answer
               ready = true;
            }
         }
         while (!ready);

         return result;
      }
   }
}
