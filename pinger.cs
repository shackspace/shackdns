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

class Program
{
  static Database DB = null;

  static void ReloadDatabase(string databasePath)
  {
    var matcher = new Regex(@"(?<name>[\w\-\.]+)\s*IN\s*A\s*(?<ip>\d+\.\d+\.\d+\.\d+)", RegexOptions.None);
    var db = new Database();
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

    DB = db;
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
      var db = DB; // get local copy

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

  static void ServeHTTP()
  {
    var listener = new HttpListener();
    listener.Prefixes.Add("http://localhost:8080/");
    listener.Start();

    while (true)
    {
      var ctx = listener.GetContext();
      Console.WriteLine("Serving {0}", ctx.Request.Url.AbsolutePath);
      using (ctx.Response)
      {
        switch (ctx.Request.Url.AbsolutePath)
        {
          case "/":
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentType = "text/html";
            using (var sw = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8))
            {
              sw.WriteLine(HTMLTemplate.prefix);
              var db = DB; // local copy
              foreach (var client in db.Hosts)
              {
                var host = client.Value;
                sw.WriteLine("<div class=\"entry\">");
                sw.WriteLine("<a class=\"hostname\" target=\"_blank\" href=\"http://{0}.shack\">{0}.shack</a>", host.Name);

                var ls = host.LastSeen;

                sw.WriteLine("<div class=\"last-seen\">{0}</div>", ls?.ToString() ?? "-");
                foreach (var address in host.Addresses)
                {
                  lock (address)
                  {
                    if (address.Status == IPStatus.Success && address.RoundtripTime != null)
                    {
                      sw.WriteLine("<div class=\"ip online\">{0}</div>", address.IP);
                    }
                    else if (address.Status != IPStatus.Success)
                    {
                      sw.WriteLine("<div class=\"ip offline\" title=\"{1}\">{0}</div>", address.IP, address.Status);
                    }
                    else
                    {
                      sw.WriteLine("<div class=\"ip unobserved\">{0}</div>", address.IP);
                    }
                  }
                }
                sw.WriteLine("</div>");
              }
              sw.WriteLine(HTMLTemplate.postfix);
            }
            break;

          default:
            ctx.Response.StatusCode = 404;
            break;
        }
      }
    }
  }

  // pinger.exe Bind-Config Service-Port
  static void Main(string[] args)
  {
    ReloadDatabase(args[0]);

    var updaterThread = new Thread(RefreshAddresses)
    {
      Name = "Ping Thread",
    };
    updaterThread.Start();

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
    if (result.Address != this.IP)
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

class Database
{
  public IDictionary<string, Host> Hosts { get; } = new Dictionary<string, Host>();
}


class HTMLTemplate
{
  internal const string prefix = @"<!doctype html>
<html>

<head>
  <meta charset=""utf-8"">
  <title>services.shack</title>
  <style type=""text/css"">
    h1 {}

    div.entry {
      border-bottom: 1px solid #CCC;
      padding: 0;
      box-sizing: border-box;
    }

    a.hostname {
      padding: 6px;
      width: 20em;
      box-sizing: border-box;
      display: inline-block;
      text-align: right;
      border-right: 1px solid #CCC;
      padding-right: 4px;
    }

    .last-seen {
      padding: 6px;
      width: 10em;
      box-sizing: border-box;
      display: inline-block;
      text-align: center;
      border-right: 1px solid #CCC;
      padding-right: 4px;
    }

    div.ip {
      border: 1px solid black;
      background-color: yellow;
      width: 10em;
      font-family: monospace;
      padding: 3px;
      display: inline-block;
      margin: 0;
      box-sizing: border-box;
      text-align: center;
    }

    div.ip.unobserved {
      background-color: yellow;
      color: black;
    }

    div.ip.offline {
      background-color: red;
      color: white;
    }

    div.ip.online {
      background-color: green;
      color: white;
    }
  </style>
</head>

<body>

  <h1>services.shack</h1>
  <div class=""entry"">
    <a class=""hostname"" target=""_blank"" style=""text-align: center"">Hostname</a>
    <div class=""last-seen"" style=""text-align: center"">Last Seen</div>
    <div class="""" style=""padding-left: 4px; display: inline-block"">IP Addresses</div>
  </div>
";

  internal const string postfix = @"
</body>

</html>";
}