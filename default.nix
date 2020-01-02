with (import <nixpkgs> {});
derivation {
  name = "shackDNS";
  builder = "${bash}/bin/bash";
  args = [ ./build.sh ];
  inherit mono6 coreutils ;
  src = fetchgit {
		url = "https://git.shackspace.de/rz/shackdns";
		rev = "e55cc906c734b398683f9607b93f1ad6435d8575";
		sha256 = "1hkwhf3hqb4fz06b1ckh7sl0zcyi4da5fgdlksian8lxyd19n8sq";
	};
	buildInputs = [ ];
  system = builtins.currentSystem;
}
