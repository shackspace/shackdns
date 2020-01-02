with (import <nixpkgs> {});
derivation {
  name = "shackDNS";
  builder = "${bash}/bin/bash";
  args = [ ./build.sh ];
  inherit mono6 coreutils ;
  src = fetchgit {
		url = "https://git.shackspace.de/rz/shackdns";
		rev = "6ea48131b06bb7cc039fe176830e54c28db28c";
		sha256 = "0p6a03gk4flk6nb0l8wbnshymy11fpf4m8wf89m9rca85i8d84rw";
	};
	buildInputs = [ ];
  system = builtins.currentSystem;
}
