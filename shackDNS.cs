using System;
using System.Net;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

class Program
{
  static void ReloadDatabase(string databasePath)
  {
    var matcher = new Regex(@"(?<name>[\w\-\.]+)\s*IN\s*A\s*(?<ip>\d+\.\d+\.\d+\.\d+)", RegexOptions.None);
    var db = new Services();
    foreach (var line in File.ReadAllLines(databasePath))
    {
      if (line.StartsWith(";"))
        continue;

      var m = matcher.Match(line);
      if (!m.Success)
        continue;

      var name = m.Groups["name"].Value;

      Host host;
      if (!db.Hosts.TryGetValue(name, out host))
        db.Hosts.Add(name, (host = new Host(name)));

      host.Addresses.Add(new Address(IPAddress.Parse(m.Groups["ip"].Value)));
    }

    Database.SetServices(db);
  }

  const string DateFormat = "ddd MMM dd yyyy HH:mm:ss  (CET)";
  //                         Mon Dec 23 2019 16:19:38  (CET)

  static void ReloadLeases()
  {
    var client = new WebClient();
    var pings = new ConcurrentQueue<Ping>();

    for (int i = 0; i < 10; i++)
    {
      var ping = new Ping();
      ping.PingCompleted += (sender, ea) =>
      {
        // update the state object and
        // return the ping object to the pool
        var entry = (DhcpEntry)ea.UserState;
        try
        {
          entry.Update(ea.Reply);
        }
        catch (Exception ex)
        {
          Console.WriteLine("{0}: {1} != {2}", ex.Message, ea.Reply.Address, entry.IP);
        }
        pings.Enqueue((Ping)sender);
      };
      pings.Enqueue(ping);
    }

    while (true)
    {
      try
      {
        var jsonCode = client.DownloadString("http://leases.shack/api/leases");

        var array = (JArray)JValue.Parse(jsonCode);

        // {
        //   "ip": "10.42.29.43",
        //   "starts": "Mon Dec 23 2019 15:59:38  (CET)",
        //   "ends": "Mon Dec 23 2019 16:19:38  (CET)",
        //   "cltt": "Mon Dec 23 2019 15:59:38  (CET)",
        //   "bindingState": "active",
        //   "nextBindingState": "free",
        //   "rewindBindingState": "free",
        //   "hardwareEthernet": "00:27:22:6a:00:1b",
        //   "uid": "\\001\\000'\\\"j\\000\\033",
        //   "clientHostname": "hackerpig"
        // },

        var oldState = Database.GetDhcp();

        var leases = new List<DhcpEntry>();

        foreach (JObject jEntry in array)
        {
          var macString = (string)jEntry["hardwareEthernet"];
          if (macString == null)
            continue;

          var mac = PhysicalAddress.Parse(macString.Replace(":", "-").ToUpper());

          var entry = oldState.Entries.FirstOrDefault(e => e.MAC.Equals(mac));
          if (entry == null)
          {
            entry = new DhcpEntry
            {
              MAC = mac,
            };
          }

          entry.DeviceName = (string)jEntry["clientHostname"];
          entry.IP = IPAddress.Parse((string)jEntry["ip"]);
          entry.FirstLease = DateTime.ParseExact((string)jEntry["starts"], DateFormat, CultureInfo.InvariantCulture);
          entry.LastRefresh = DateTime.ParseExact((string)jEntry["ends"], DateFormat, CultureInfo.InvariantCulture);

          if (leases.FirstOrDefault(l => l.MAC == entry.MAC) != null)
          {
            // Console.WriteLine("Double Mac: {0}", entry.MAC);
          }
          else
          {
            leases.Add(entry);
          }
        }

        leases.Sort((a, b) => String.Compare(a.DeviceName, b.DeviceName));

        Database.SetDhcp(new Dhcp
        {
          Entries = leases.ToArray(),
        });

        foreach (var lease in leases)
        {
          Ping ping;
          while (pings.TryDequeue(out ping) == false)
          {
            Thread.Sleep(1);
          }
          ping.SendAsync(lease.IP, 250, lease);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine("Failed to fetch leases: {0}", ex);
      }
      Thread.Sleep(2000);
    }
  }

  static void RefreshShackles()
  {
    while (true)
    {
      var shackles = Database.GetShackles();
      var dhcp = Database.GetDhcp();
      foreach (var shackie in shackles.Shackies)
      {
        shackie.IsOnline = shackie.MACs.Any(mac => dhcp.Entries.FirstOrDefault(d => d.MAC.Equals(mac))?.RoundtripTime != null);
      }
      Thread.Sleep(100);
    }
  }

  static void RefreshAddresses()
  {
    // use a pool of ping objects to allow concurrent
    // pinging. a pool is required as sequential pings are 
    // too slow and pinging all at once kills the local perf
    // and eats up too much resource.
    var pings = new ConcurrentQueue<Ping>();

    // we allow a maximum of 10 concurrent pings
    for (int i = 0; i < 10; i++)
    {
      var ping = new Ping();
      ping.PingCompleted += (sender, ea) =>
      {
        // update the state object and
        // return the ping object to the pool
        ((Address)ea.UserState).Update(ea.Reply);
        pings.Enqueue((Ping)sender);
      };
      pings.Enqueue(ping);
    }

    while (true)
    {
      var db = Database.GetServices(); // get local copy

      var timeout = DateTime.Now + TimeSpan.FromSeconds(5);

      // Console.WriteLine("Starting update...");
      foreach (var client in db.Hosts)
      {
        foreach (var ip in client.Value.Addresses)
        {
          Ping ping;
          while (pings.TryDequeue(out ping) == false)
          {
            Thread.Sleep(1);
          }

          ping.SendAsync(ip.IP, 250, ip);
        }
      }

      while (DateTime.Now > timeout)
      {
        Thread.Sleep(100);
      }
    }
  }

  static string Slurp(string fileName)
  {
    return File.ReadAllText(fileName, Encoding.UTF8);
  }

  static string GetMimeType(string fileName)
  {
    switch (Path.GetExtension(fileName))
    {
      case ".js": return "text/javascript";
      default: return System.Web.MimeMapping.GetMimeMapping(fileName);
    }
  }

  static void Serve(HttpListenerResponse response, string fileName)
  {
    response.ContentEncoding = Encoding.UTF8;
    response.ContentType = GetMimeType(fileName);
    using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read))
    {
      fs.CopyTo(response.OutputStream);
    }
    response.OutputStream.Close();
  }

  class LiveDataSet
  {
    public JArray dhcp;
    public JArray services;

    public JArray shackles;
  }

  static LiveDataSet CreateDataset()
  {
    var servicesData = Database.GetServices(); // copy local ref
    var dhcpData = Database.GetDhcp();

    var dhcp = new JArray();
    foreach (var lease in dhcpData.Entries)
    {
      dhcp.Add(new JObject
      {
        ["mac"] = BitConverter.ToString(lease.MAC.GetAddressBytes()).ToString(),
        ["ip"] = lease.IP.ToString(),
        ["firstLease"] = lease.FirstLease.ToString(),
        ["lastRefresh"] = lease.LastRefresh.ToString(),
        ["deviceName"] = lease.DeviceName,
        ["status"] = lease.Status.ToString(),
        ["ping"] = lease.RoundtripTime,
        ["lastSeen"] = lease.LastSeen.ToString(),
      });
    }

    var services = new JArray { };
    foreach (var item in servicesData.Hosts)
    {
      var host = item.Value;
      lock (host)
      {
        services.Add(new JObject
        {
          ["name"] = host.Name,
          ["dns"] = host.Name + ".shack",
          ["lastSeen"] = host.LastSeen?.ToString() ?? "-",
          ["addresses"] = new JArray(host.Addresses.Select(a => new JObject
          {
            ["ip"] = a.IP.ToString(),
            ["status"] = a.Status.ToString(),
            ["ping"] = a.RoundtripTime,
            ["lastSeen"] = a.LastSeen.ToString(),
          }).ToArray()),
        });
      }
    }


    var shackles = new JArray();

    var shacklesData = Database.GetShackles();
    foreach (var shackie in shacklesData.Shackies)
    {
      shackles.Add(new JObject
      {
        ["name"] = shackie.Name,
        ["online"] = shackie.IsOnline,
      });
    }

    return new LiveDataSet
    {
      shackles = shackles,
      services = services,
      dhcp = dhcp
    };
  }

  static void ServeHTTP()
  {
    var listener = new HttpListener();
    listener.Prefixes.Add("http://localhost:8080/");
    listener.Prefixes.Add("http://*:8080/");
    listener.Start();

    var format = Formatting.Indented; // .None for minified

    while (true)
    {
      var ctx = listener.GetContext();
      // Console.WriteLine("Serving {0}", ctx.Request.Url.AbsolutePath);

      using (ctx.Response)
      {
        try
        {
          switch (ctx.Request.Url.AbsolutePath)
          {
            case "/":
              Serve(ctx.Response, "frontend/index.htm");
              break;

            case "/data.json":
              ctx.Response.ContentEncoding = Encoding.UTF8;
              ctx.Response.ContentType = "text/json";
              using (var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8))
              {
                var data = CreateDataset();
                sw.WriteLine("{0}", new JObject
                {
                  ["dhcp"] = data.dhcp,
                  ["services"] = data.services,
                  ["shackles"] = data.shackles,
                }.ToString(format));
              }
              break;

            case "/data.js":
              ctx.Response.ContentEncoding = Encoding.UTF8;
              ctx.Response.ContentType = "text/javascript";
              using (var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8))
              {
                var data = CreateDataset();
                sw.WriteLine("// generated code");
                sw.WriteLine("var DHCP = {0};", data.dhcp.ToString(format));
                sw.WriteLine("var Services = {0};", data.services.ToString(format));
                sw.WriteLine("var Shackles = {0};", data.shackles.ToString(format));
              }
              break;

            case "/style.css":
              Serve(ctx.Response, "frontend/style.css");
              break;

            case "/frontend.js":
              Serve(ctx.Response, "frontend/frontend.js");
              break;

            case "/img/edit.svg":
              Serve(ctx.Response, "frontend/img/edit.svg");
              break;

            case "/img/wiki.svg":
              Serve(ctx.Response, "frontend/img/wiki.svg");
              break;

            default:
              ctx.Response.StatusCode = 404;
              break;
          }
        }
        catch (FileNotFoundException ex)
        {
          try
          {
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentType = "text/plain";
            using (var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8))
            {
              sw.WriteLine("{0} not found!", ex.FileName);
            }
          }
          catch (Exception)
          {
            Console.WriteLine("Error while serving error.");
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
      }
    }
  }

  static void LoadShacklesDB(string fileName)
  {
    var array = (JArray)JToken.Parse(Slurp(fileName));
    var shackies = new List<Shackie>();
    foreach (JObject user in array)
    {
      shackies.Add(new Shackie
      {
        Name = (string)user["user"],
        IsOnline = false,
        MACs = user["ids"].Where(i => (string)i["type"] == "mac").Select(i => PhysicalAddress.Parse(((string)i["value"]).Replace(":", "-").ToUpper())).ToArray(),
      });
    }
    Database.SetShackles(new Shackles
    {
      Shackies = shackies.ToArray()
    });
  }

  // pinger.exe Bind-Config Service-Port
  static void Main(string[] args)
  {
    ReloadDatabase(args[0]);
    LoadShacklesDB("shackles.json");

    var updaterThread = new Thread(RefreshAddresses)
    {
      Name = "Ping Thread",
    };
    updaterThread.Start();

    var leasesThread = new Thread(ReloadLeases)
    {
      Name = "Leases-Thread",
    };
    leasesThread.Start();

    var shacklesThread = new Thread(RefreshShackles)
    {
      Name = "Shackles-Thread",
    };
    shacklesThread.Start();

    var serviceThread = new Thread(ServeHTTP)
    {
      Name = "HTTP-Thread",
    };
    serviceThread.Start();
  }
}

// IP address of a host, stores all status meta data as well.
class Address
{
  public Address(IPAddress ip)
  {
    this.IP = ip;
  }

  public void Update(PingReply result)
  {
    if (!result.Address.Equals(this.IP))
      throw new ArgumentException("Invalid reply for this address!");
    lock (this)
    {
      this.Status = result.Status;
      if (result.Status == IPStatus.Success)
      {
        this.LastSeen = DateTime.Now;
        this.RoundtripTime = result.RoundtripTime;
      }
      else
      {
        this.RoundtripTime = null;
      }
    }
  }

  public override int GetHashCode()
  {
    return this.IP.GetHashCode();
  }

  public override bool Equals(object obj)
  {
    if (obj is Address address)
      return address.IP == this.IP;
    else
      return false;
  }

  public override string ToString()
  {
    return string.Format("{0}: {1}", this.IP, RoundtripTime);
  }

  public IPAddress IP { get; }

  // if not null, then contains the roundtrip time in ms since
  // the last ping.
  public long? RoundtripTime { get; private set; }

  // contains the status of the last ping
  public IPStatus Status { get; private set; }

  // contains the last 
  public DateTime? LastSeen { get; private set; }
}

// An A entry in the DNS database
class Host
{
  public Host(string host)
  {
    this.Name = host;
  }

  // gets the name of this host.
  public string Name { get; }

  // returns a set of IP addresses this host hast.
  public ISet<Address> Addresses { get; } = new HashSet<Address>();

  // returns the time this host was last seen.
  public DateTime? LastSeen => this.Addresses
    .Where(a => a.LastSeen != null)
    .Select(a => a.LastSeen)
    .OrderByDescending(dt => dt)
    .Select(dt => (DateTime?)dt)
    .FirstOrDefault();
}

class DhcpEntry
{
  public string DeviceName { get; set; }

  public PhysicalAddress MAC { get; set; }

  public IPAddress IP { get; set; }

  public DateTime FirstLease { get; set; }

  public DateTime LastRefresh { get; set; }


  public void Update(PingReply result)
  {
    if (!result.Address.Equals(this.IP))
      throw new ArgumentException(string.Format("Invalid reply for this address: {0}, {1}", this.IP, result.Address));
    lock (this)
    {
      this.Status = result.Status;
      if (result.Status == IPStatus.Success)
      {
        this.LastSeen = DateTime.Now;
        this.RoundtripTime = result.RoundtripTime;
      }
      else
      {
        this.RoundtripTime = null;
      }
    }
  }
  public long? RoundtripTime { get; private set; }

  public IPStatus Status { get; private set; }

  public DateTime? LastSeen { get; private set; }
}

class Services
{
  public IDictionary<string, Host> Hosts { get; } = new Dictionary<string, Host>();
}

class Dhcp
{
  public DhcpEntry[] Entries { get; set; } = new DhcpEntry[0];
}

class Shackie
{
  public string Name { get; set; }

  public PhysicalAddress[] MACs { get; set; } = new PhysicalAddress[0];

  public bool IsOnline { get; set; }
}

class Shackles
{
  public Shackie[] Shackies { get; set; } = new Shackie[0];
}

static class Database
{
  private static Services services = new Services();

  private static Dhcp dhcp = new Dhcp();

  private static Shackles shackles = new Shackles();

  public static Services GetServices() => services;

  public static Dhcp GetDhcp() => dhcp;

  public static Shackles GetShackles() => shackles;

  public static void SetServices(Services list)
  {
    services = list;
  }

  public static void SetDhcp(Dhcp list)
  {
    dhcp = list;
  }

  public static void SetShackles(Shackles list)
  {
    shackles = list;
  }
}
