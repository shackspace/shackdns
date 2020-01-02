export PATH="$mono6/bin:$coreutils/bin:$buildInputs/bin"

function die()
{
  echo "$@" 
  exit 1
}

mkdir $out || die "failed to create $out"

SRCDLL=$src/Newtonsoft.Json.dll

cp ${SRCDLL} $out/ || die "failed to copy Newtonsoft.Json.dll"

cp -r ${src}/frontend $out

mcs /out:$out/shackDNS.exe /optimize /r:System.Web.dll /r:${SRCDLL} $src/shackDNS.cs || die "failed to compile shackDNS.cs"
