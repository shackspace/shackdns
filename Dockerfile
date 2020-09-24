FROM mono:latest

WORKDIR /shackdns

# mount your config file (shackDNS.cfg) and other necessary files into this directory
VOLUME /shackdns/config

# This is the default port. Note that you might override this via `binding` in your config file!
EXPOSE 80/tcp

# Copy the source over and compile the project
COPY src/ ./src/
COPY libs/*.dll ./
COPY data/mac-prefixes.tsv mac-prefixes.tsv
COPY frontend/ ./frontend/

RUN [ "mcs", "/sdk:4.5", "/out:shackDNS.exe", "/optimize", "/r:System.Data.dll", "/r:System.Web.dll","/resource:mac-prefixes.tsv,MacData.tsv","/r:Newtonsoft.Json.dll","/r:Emitter.dll","src/DeviceTree.cs","src/shackDNS.cs" ]

RUN [ "rm", "-rf", "src/", "mac-prefixes.tsv" ]

ENTRYPOINT [ "mono", "shackDNS.exe", "/shackdns/config/shackDNS.cfg" ]
