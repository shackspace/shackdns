
// setup simple error handler for alerting error messages
// instead of silently ignoring them
window.addEventListener('error', (e) => alert(e.message))

function $(query) {
  const elem = document.querySelector(query);
  if (elem == null)
    throw "Element '" + query + "' not found!";
  return elem;
}

function setActivePane(name) {
  const buttons = Array.from(document.querySelectorAll("button.checked"));
  for (const i in buttons) {
    buttons[i].classList.remove("checked");
  }

  const panes = Array.from(document.querySelectorAll("section.enabled"));
  for (const i in panes) {
    panes[i].classList.remove("enabled");
  }

  $('#' + name + '-button').classList.add("checked");
  $('#' + name + '-pane').classList.add("enabled");

  document.location.hash = name;
}

function initDhcpPane() {
  const table = $('#dhcp-pane table tbody');

  for (var i = 0; i < table.children.length;) {
    const child = table.children[i];

    if (child.classList.contains("entry")) {
      table.removeChild(child);
    } else {
      i += 1;
    }
  }

  for (const i in DHCP) {
    const lease = DHCP[i];

    const row = document.createElement("tr");
    row.classList.add("entry");
    function addCell(init) {
      const cell = document.createElement("td");
      cell.innerText = init || "";
      row.appendChild(cell);
      return cell;
    }

    addCell(lease.clientHostname || "-");

    const mac = addCell();
    {
      const a = document.createElement("a");
      a.href = 'http://' + lease.hardwareEthernet.split(":").join("-") + '.device.shack';
      a.innerText = lease.hardwareEthernet;
      a.target = "_blank";
      a.classList.add("mac");
      mac.appendChild(a);
    }

    const ip = addCell();
    {
      const span = document.createElement("span");
      span.classList.add("ip");
      if (lease.status == "Success") {
        if (lease.ping != null) {
          span.classList.add("online");
          span.title = String(lease.ping) + " ms";
        } else {
          span.classList.add("unobserved");
          span.title = "Unobserved";
        }
      } else {
        span.classList.add("offline");
        span.title = lease.status;
      }
      span.innerText = lease.ip;
      ip.appendChild(span);
    }

    addCell(lease.starts);
    addCell(lease.cltt);

    table.appendChild(row);
  }
}

function initServicePane() {
  const table = $('#services-pane table tbody');

  for (var i = 0; i < table.children.length;) {
    const child = table.children[i];

    if (child.classList.contains("entry")) {
      table.removeChild(child);
    } else {
      i += 1;
    }
  }

  for (const i in Services) {
    const svc = Services[i];

    const row = document.createElement("tr");
    row.classList.add("entry");

    function addCell(init) {
      const cell = document.createElement("td");
      cell.innerText = init || "";
      row.appendChild(cell);
      return cell;
    }

    addCell(svc.name);
    addCell("contact");

    const dns = addCell();
    {
      const a = document.createElement("a");
      a.target = "_blank";
      a.href = 'http://' + svc.dns;
      a.innerText = svc.dns;
      dns.appendChild(a);
    }

    const ips = addCell();
    for (const j in svc.addresses) {
      const ip = svc.addresses[j];
      const span = document.createElement("span");
      span.classList.add("ip");
      if (ip.status == "Success") {
        if (ip.ping != null) {
          span.classList.add("online");
          span.title = String(ip.ping) + " ms";
        } else {
          span.classList.add("unobserved");
          span.title = "Unobserved";
        }
      } else {
        span.classList.add("offline");
        span.title = ip.status;
      }
      span.innerText = ip.ip;
      ips.appendChild(span);
    }

    addCell(svc.lastSeen);

    addCell("availability");

    const tools = addCell("");
    {
      const wiki = document.createElement("a");
      wiki.classList.add("link");
      wiki.classList.add("wiki");
      wiki.href = "javascript:alert('wiki me')";
      wiki.innerText = " ";
      wiki.title = "Dokumentation";
      wiki.target = "_blank";
      tools.appendChild(wiki);

      const edit = document.createElement("a");
      edit.classList.add("link");
      edit.classList.add("edit");
      edit.href = "javascript:alert('edit me')";
      edit.innerText = " ";
      edit.title = "Bearbeiten";
      edit.target = "_blank";
      tools.appendChild(edit);
    }

    table.appendChild(row);
  }
}

function initializeServices() {

  // initialize service pane

  initDhcpPane();
  initServicePane();

  if (document.location.hash != "") {
    setActivePane(document.location.hash.substr(1));
  }
  console.log("init done.");
}