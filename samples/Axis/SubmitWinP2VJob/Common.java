package SubmitWinP2VJob;

import com.vmware.converter.*;

public class Common
{
   static public ConverterComputerSpecLiveComputerLocation
   buildLiveSourceLocation(String sourceName, String sourceUsername, String sourcePassword,
                           String sourceOsType, String sslThumbprint)
   {
      ConverterComputerSpecLiveComputerLocation liveSourceLocation =
                                      new ConverterComputerSpecLiveComputerLocation();
      liveSourceLocation.setHostname(sourceName);
      liveSourceLocation.setUsername(sourceUsername);
      liveSourceLocation.setPassword(sourcePassword);
      liveSourceLocation.setOsType(sourceOsType);
      liveSourceLocation.setVerifyPeer(true);
      liveSourceLocation.setSslThumbprint(sslThumbprint);

      return liveSourceLocation;
   }
}
