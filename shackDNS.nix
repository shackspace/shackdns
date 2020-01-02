{ config, lib, pkgs, ... }:

let
  pkg = pkgs.callPackage (
    pkgs.fetchgit {
      url = "https://git.shackspace.de/rz/shackdns";
      rev = "6ea48131b06bb7cc039fe176830e54c28db28c";
      sha256 = "0p6a03gk4flk6nb0l8wbnshymy11fpf4m8wf89m9rca85i8d84rw";
	  });
    home = "/var/lib/shackDNS";
    port = "8083";
in {
  # receive response from light.shack / standby.shack
  # networking.firewall.allowedUDPPorts = [ 2342 ];

  users.users.shackDNS = {
    inherit home;
    createHome = true;
  };
  services.nginx.virtualHosts."leases.shack" = {
    locations."/" = {
      proxyPass = "http://localhost:${port}/";
    };
  };
  services.nginx.virtualHosts."shackdns.shack" = {
    locations."/" = {
      proxyPass = "http://localhost:${port}/";
    };
  };
  services.nginx.virtualHosts."shackles.shack" = {
    locations."/" = {
      proxyPass = "http://localhost:${port}/";
    }
  };

  systemd.services.shackDNS = {
    description = "shackDNS provides an overview over DHCP and DNS as well as a replacement for shackles";
    wantedBy = [ "multi-user.target" ];
    environment.PORT = port;
    serviceConfig = {
      User = "shackDNS";
      WorkingDirectory = home;
      ExecStart = "${pkgs.nixos.mono6}/bin/mono ${pkg}/shackDNS.exe";
      PrivateTmp = true;
      Restart = "always";
      RestartSec = "15";
    };
  };
}
