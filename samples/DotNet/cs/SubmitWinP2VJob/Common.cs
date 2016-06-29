using System;
using System.Collections.Generic;
using System.Text;
using ConverterApi;

namespace VMConverter {

    class Common {

        // LiveComputerLocation specifies the location of a live physical machine as well as the credentials to access it. 
        public static ConverterComputerSpecLiveComputerLocation BuildLiveSourceLocation(String sourceName, String sourceUsername, String sourcePassword, String osType, String sslThumbprint) {

            ConverterComputerSpecLiveComputerLocation liveSourceLocation = new ConverterComputerSpecLiveComputerLocation();
            liveSourceLocation.hostname = sourceName;
            liveSourceLocation.username = sourceUsername;
            liveSourceLocation.password = sourcePassword;
            liveSourceLocation.osType = osType;
            liveSourceLocation.verifyPeer = true;
            liveSourceLocation.verifyPeerSpecified = true;
            liveSourceLocation.sslThumbprint = sslThumbprint;

            return liveSourceLocation;
        }
    }
}
