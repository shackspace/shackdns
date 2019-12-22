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
    // we allow a maximum of 10 concurrent pings
    var pings = new ConcurrentQueue<Ping>();
    for (int i = 0; i < 10; i++)
    {
      var ping = new Ping();
      ping.PingCompleted += (sender, ea) =>
      {
        ((Address)ea.UserState).Update(ea.Reply);
        pings.Enqueue((Ping)sender);
      };
      pings.Enqueue(ping);
    }

    while (true)
    {
      var db = DB; // get local copy

      var timeout = DateTime.Now + TimeSpan.FromSeconds(5);

      Console.WriteLine("Starting update...");
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
      this.RoundtripTime = (result.Status == IPStatus.Success) ? (long?)result.RoundtripTime : null;
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

  public long? RoundtripTime { get; private set; }

  public IPStatus Status { get; private set; }
}

class Host
{
  public Host(string host)
  {
    this.Name = host;
  }

  public string Name { get; }

  public ISet<Address> Addresses { get; } = new HashSet<Address>();
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

    div.ip {
      border: 1px solid black;
      background-color: yellow;
      width: 8em;
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
";

  internal const string postfix = @"
</body>

</html>";
}