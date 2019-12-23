
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

function cleanTable(table) {

  for (var i = 0; i < table.children.length;) {
    const child = table.children[i];

    if (child.classList.contains("entry")) {
      table.removeChild(child);
    } else {
      i += 1;
    }
  }
}

function initDhcpPane() {
  const table = $('#dhcp-pane table tbody');
  cleanTable(table);

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

    addCell(lease.deviceName || "-");

    const mac = addCell();
    {
      const a = document.createElement("a");
      a.href = 'http://' + lease.mac + '.device.shack';
      a.innerText = lease.mac.split("-").join(":");
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

    addCell(lease.firstLease);
    addCell(lease.lastRefresh);

    table.appendChild(row);
  }
}

function initServicePane() {
  const table = $('#services-pane table tbody');
  cleanTable(table);

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

function initShacklesPane() {
  const table = $('#shackles-pane table tbody');
  cleanTable(table);

  for (const i in Shackles) {
    const shackie = Shackles[i];

    const row = document.createElement("tr");
    row.classList.add("entry");
    function addCell(init) {
      const cell = document.createElement("td");
      cell.innerText = init || "";
      row.appendChild(cell);
      return cell;
    }

    addCell(shackie.name);

    const state = addCell();
    {
      const span = document.createElement("span");
      span.classList.add("status");

      if (shackie.online) {
        span.classList.add("online");
        span.innerText = "Yes";
      } else {
        span.classList.add("offline");
        span.innerText = "No";
      }

      state.appendChild(span);
    }

    table.appendChild(row);
  }
}

function reloadData() {
  initDhcpPane();
  initServicePane();
  initShacklesPane();
}

var isReloading = false;

function liveReloadData() {
  if (isReloading) {
    return;
  }
  isReloading = true;

  const btn = $('#refresh-button');
  btn.classList.add("loading");

  var xhttp = new XMLHttpRequest();
  xhttp.onreadystatechange = function () {
    // console.log(this.readyState, this.status);
    if (this.readyState == 4 && this.status == 200) {
      btn.classList.remove("loading");
      const data = JSON.parse(this.responseText);

      Services = data.services;
      Shackles = data.shackles;
      DHCP = data.dhcp;

      reloadData();

      isReloading = false;
    }
  };
  xhttp.open("GET", "data.json", true);
  xhttp.send();
}

function initializeServices() {

  // initialize service pane
  reloadData();

  if (document.location.hash != "") {
    setActivePane(document.location.hash.substr(1));
  }

  setInterval(liveReloadData, 10000);

  console.log("init done.");
}