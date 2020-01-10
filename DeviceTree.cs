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
using System.Collections.Specialized;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

class Infrastructure
{
  public static Infrastructure Deserialize(JArray items)
  {
    var infra = new Infrastructure();

    foreach (JObject obj in items)
    {
      var dev = DnsEntry.Deserialize(obj);
      if (dev is PhysicalDevice pa)
        infra.Devices.Add(pa);
      else
        throw new InvalidOperationException("Only physical devices may be on the root level!");
    }

    return infra;
  }

  public void CreateGraph(StreamWriter writer)
  {
    writer.WriteLine("<infra>");
    foreach (var dev in this.Devices)
    {
      Render(writer, dev);
    }
    writer.WriteLine("</infra>");
  }

  private static void Render(StreamWriter writer, Device dev)
  {
    writer.WriteLine("<device>", Math.Abs(dev.GetHashCode()));
    writer.WriteLine("<header>");
    writer.WriteLine("<icon class=\"{0}\"></icon>", dev.Type.ToString().ToLower());
    writer.WriteLine("<name>{0}</name>", dev.Name);
    writer.WriteLine("</header>");
    if (dev.Services.Count > 0)
    {
      writer.Write("<services>");
      foreach (var svc in dev.Services)
      {
        writer.WriteLine("<service><name>{0}</name></service>", svc.Name);
      }
      writer.Write("</services>");
    }
    if (dev.Children.Count > 0)
    {
      writer.Write("<children>");
      foreach (var child in dev.Children)
      {
        Render(writer, child);
      }
      writer.Write("</children>");
    }
    writer.WriteLine("<footer>");
    foreach (var ip in dev.Addresses)
      writer.WriteLine("<ip>{0}</ip>", ip);
    writer.WriteLine("</footer>");
    writer.WriteLine("</device>");
  }

  public ICollection<PhysicalDevice> Devices { get; } = new List<PhysicalDevice>();
}

enum ExpectedState
{
  Offline,
  Online,
  OnlineWhenOpen,
}

abstract class DnsEntry
{
  public static DnsEntry Deserialize(JObject obj)
  {
    var mapper = new Dictionary<string, Func<DnsEntry>>()
    {
      ["service"] = () => new Service(),
      ["server"] = () => new PhysicalDevice(DeviceType.Server),
      ["endpoint"] = () => new PhysicalDevice(DeviceType.Endpoint),
      ["iot"] = () => new PhysicalDevice(DeviceType.IoT),
      ["special"] = () => new PhysicalDevice(DeviceType.Special),
      ["vm"] = () => new VirtualDevice(DeviceType.VM),
      ["container"] = () => new VirtualDevice(DeviceType.Container),
    };

    var type = (string)obj["type"];
    DnsEntry result = mapper[type]();
    result.OnDeserialize(obj);
    return result;
  }

  protected virtual void OnDeserialize(JObject obj)
  {
    this.Name = (string)obj["name"];
    if (this.Name != null)
      this.DomainNames.Add(this.Name);

    if (obj["dns"] is JValue dnsName)
    {
      this.DomainNames.Clear();
      this.DomainNames.Add((string)dnsName);
    }
    else if (obj["dns"] is JArray dnsNames)
    {
      this.DomainNames.Clear();
      foreach (string name in dnsNames)
        this.DomainNames.Add(name);
    }

    this.Description = (string)obj["description"];
    this.Contact = (string)obj["contact"];

    if (obj["wiki"] is JValue wikiStr)
      this.WikiLink = new Uri((string)wikiStr);

    if (obj["expected"] is JValue expectedStr)
    {
      switch ((string)expectedStr)
      {
        case "online":
          this.ExpectedState = ExpectedState.Online;
          break;
        case "offline":
          this.ExpectedState = ExpectedState.Offline;
          break;
        case "online-when-open":
          this.ExpectedState = ExpectedState.OnlineWhenOpen;
          break;
        default:
          throw new InvalidOperationException();
      }
    }
  }

  /// What's the name of this device?
  public string Name { get; set; }

  /// How can i contact someone who knows shit?
  public string Contact { get; set; }

  /// where can i find documentation?
  public Uri WikiLink { get; set; }

  /// is this service/device to be expected online, offline or only 
  /// online when the shack is open?
  public ExpectedState ExpectedState { get; set; } = ExpectedState.Online;

  /// which domain names should be bound to this IP?
  public ICollection<string> DomainNames { get; } = new HashSet<string>();

  /// what is this?
  public string Description { get; set; }
}

enum DeviceType
{
  Server,
  Endpoint,
  IoT,
  Special,
  Container,
  VM,
};

/// everything with an IP address
abstract class Device : DnsEntry
{
  private readonly List<Service> services = new List<Service>();
  private readonly List<VirtualDevice> children = new List<VirtualDevice>();
  private readonly List<IPAddress> addresses = new List<IPAddress>();

  protected Device(DeviceType type)
  {
    this.Type = type;
  }

  protected override void OnDeserialize(JObject obj)
  {
    base.OnDeserialize(obj);

    var ipField = obj["ip"];
    if (ipField == null)
      throw new InvalidOperationException($"ip is required for {(Name ?? "unknown")}!");
    else if (ipField is JValue ipStr)
      this.addresses.Add(IPAddress.Parse((string)ipStr));
    else if (ipField is JArray ipArr)
    {
      foreach (JValue str in ipArr)
        this.addresses.Add(IPAddress.Parse((string)str));
    }

    if (obj["platform"] is JValue platform)
      this.Platform = (string)platform;

    if (obj["children"] is JArray children)
    {
      foreach (JObject child in children)
      {
        var entry = Deserialize(child);
        if (entry is VirtualDevice dev)
          this.children.Add(dev);
        else if (entry is Service svc)
          this.services.Add(svc);
        else if (entry is PhysicalDevice)
          throw new NotSupportedException("Non-virtual device as child of physical device does not make sense!");
        else
          throw new NotSupportedException(entry.GetType().Name + " is not a supported child type");
      }
    }
  }

  /// what virtual devices are hosted on this device?
  public ICollection<VirtualDevice> Children => this.children;

  /// what services are hosted on this device?
  public ICollection<Service> Services => this.services;

  /// which IP addresses does this device have
  public ICollection<IPAddress> Addresses => this.addresses;

  /// what kind of device is this?
  public DeviceType Type { get; set; }

  /// For virtual devices: what virtualization is used (docker, qemu, lxc, ...)
  /// for physical devices: what hardware architecture is that (x86_64-linux, ...)
  public string Platform { get; set; }
}

/// real machines that can be touched by hand
class PhysicalDevice : Device
{
  public PhysicalDevice(DeviceType type) :
    base(type)
  {

  }

  protected override void OnDeserialize(JObject obj)
  {
    base.OnDeserialize(obj);

    this.Location = (string)obj["location"];

    JToken tok = obj["owner"];
    if (tok != null)
    {
      if (tok is JValue ownerStr)
      {
        this.Owner.Add((string)ownerStr);
      }
      else if (tok is JArray items)
      {
        foreach (string str in items)
          this.Owner.Add(str);
      }
      else
        throw new NotSupportedException();
    }
    else
    {
      this.Owner.Add("shack");
    }
  }

  /// Who owns the hardware
  public ICollection<string> Owner { get; set; } = new HashSet<string>();

  /// Where is the hardware located
  public string Location { get; set; }
}

/// virtual machines, containers, …
/// everything that is only defined by software
class VirtualDevice : Device
{
  public VirtualDevice(DeviceType type) :
    base(type)
  {

  }
}

/// A program/service that runs on a machine
class Service : DnsEntry
{

}