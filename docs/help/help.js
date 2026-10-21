// GodSaveTheScreen – Benutzeranleitung: Inhaltsverzeichnis, Suche, Sprungziele, Bildansicht, Design.
// Wird beim Build in die HTML-Datei eingebettet (build/HelpBundle.targets).
(function () {
  "use strict";

  var lang = document.documentElement.lang === "de" ? "de" : "en";
  var text = {
    de: { auto: "Design: automatisch", light: "Design: hell", dark: "Design: dunkel", none: "Keine Treffer", top: "Nach oben" },
    en: { auto: "Theme: automatic", light: "Theme: light", dark: "Theme: dark", none: "No matches", top: "Back to top" }
  }[lang];

  var content = document.querySelector(".content");
  var tocRoot = document.getElementById("toc");

  // ---------- Inhaltsverzeichnis aus den Überschriften (h2 > h3 > h4 mit id) ----------

  var headings = Array.prototype.slice.call(content.querySelectorAll("h2[id], h3[id], h4[id]"));
  var entries = [];
  var chapter = 0, section = 0;
  var lists = { 2: document.createElement("ol") };
  tocRoot.appendChild(lists[2]);
  var lastItem = {};

  headings.forEach(function (h) {
    var level = Number(h.tagName.substring(1));
    var number = "";
    if (level === 2) {
      chapter++; section = 0;
      number = String(chapter);
      var badge = document.createElement("span");
      badge.className = "chapter";
      badge.textContent = number.padStart(2, "0");
      h.insertBefore(badge, h.firstChild);
    } else if (level === 3) {
      section++;
      number = chapter + "." + section;
    }

    var parentList = lists[level];
    if (!parentList) {
      var owner = lastItem[level - 1];
      if (!owner) { return; }
      parentList = owner.querySelector(":scope > ol");
      if (!parentList) { parentList = document.createElement("ol"); owner.appendChild(parentList); }
    }

    var li = document.createElement("li");
    var a = document.createElement("a");
    a.href = "#" + h.id;
    var icon = h.querySelector("img.icon");
    if (number) {
      var n = document.createElement("span"); n.className = "num"; n.textContent = number; a.appendChild(n);
    } else if (icon) {
      var i = icon.cloneNode(); i.className = ""; i.alt = ""; a.appendChild(i);
    }
    var label = document.createElement("span");
    label.textContent = headingText(h);
    a.appendChild(label);
    li.appendChild(a);
    parentList.appendChild(li);
    lastItem[level] = li;
    for (var deeper = level + 1; deeper <= 4; deeper++) { delete lastItem[deeper]; }
    if (level < 4) { lists[level + 1] = null; }

    // Permalink
    var anchor = document.createElement("a");
    anchor.className = "anchor"; anchor.href = "#" + h.id; anchor.textContent = "#"; anchor.setAttribute("aria-hidden", "true");
    h.appendChild(anchor);

    entries.push({ heading: h, item: li, link: a, label: label, title: label.textContent, body: sectionText(h) });
  });

  function headingText(h) {
    var clone = h.cloneNode(true);
    clone.querySelectorAll(".chapter, .anchor, img").forEach(function (e) { e.remove(); });
    return clone.textContent.trim();
  }

  function sectionText(h) {
    var parts = [], node = h.nextElementSibling;
    while (node && !/^H[234]$/.test(node.tagName)) { parts.push(node.textContent); node = node.nextElementSibling; }
    return parts.join(" ").toLowerCase();
  }

  // ---------- Suche ----------

  var search = document.getElementById("search");
  var empty = document.createElement("p");
  empty.className = "toc-title"; empty.textContent = text.none; empty.hidden = true;
  tocRoot.appendChild(empty);

  search.addEventListener("input", function () {
    var q = search.value.trim().toLowerCase();
    var any = false;
    entries.forEach(function (e) {
      var hit = !q || e.title.toLowerCase().indexOf(q) >= 0 || e.body.indexOf(q) >= 0;
      e.item.classList.toggle("hidden", !hit);
      e.match = hit;
      e.label.innerHTML = "";
      var i = q ? e.title.toLowerCase().indexOf(q) : -1;
      if (i >= 0) {
        e.label.append(e.title.substring(0, i));
        var m = document.createElement("mark"); m.textContent = e.title.substring(i, i + q.length); e.label.append(m);
        e.label.append(e.title.substring(i + q.length));
      } else {
        e.label.textContent = e.title;
      }
      any = any || hit;
    });
    // Eltern von Treffern bleiben sichtbar; bei Suche auch die dritte Ebene zeigen.
    entries.forEach(function (e) {
      if (e.match) {
        for (var p = e.item.parentElement; p && p !== tocRoot; p = p.parentElement) {
          if (p.tagName === "LI") { p.classList.remove("hidden"); }
          if (p.tagName === "OL" && q) { p.style.display = "block"; }
        }
      }
    });
    if (!q) { tocRoot.querySelectorAll("ol ol ol").forEach(function (o) { o.style.display = ""; }); }
    empty.hidden = any;
  });

  search.addEventListener("keydown", function (ev) {
    if (ev.key === "Enter") {
      var first = entries.filter(function (e) { return e.match !== false; })[0];
      if (first) { go(first.heading.id); }
    } else if (ev.key === "Escape") {
      search.value = ""; search.dispatchEvent(new Event("input"));
    }
  });

  document.addEventListener("keydown", function (ev) {
    if ((ev.key === "/" || (ev.key === "f" && (ev.ctrlKey || ev.metaKey) && ev.shiftKey)) && document.activeElement !== search) {
      ev.preventDefault(); search.focus(); search.select();
    }
  });

  // ---------- Aktiver Abschnitt im Inhaltsverzeichnis ----------

  var current = null;
  function markActive() {
    var active = null;
    for (var i = 0; i < entries.length; i++) {
      if (entries[i].heading.getBoundingClientRect().top <= 100) { active = entries[i]; } else { break; }
    }
    if (active !== current) {
      if (current) { current.link.classList.remove("active"); }
      current = active;
      if (current) {
        current.link.classList.add("active");
        var box = tocRoot.closest(".sidebar"), r = current.link.getBoundingClientRect(), b = box.getBoundingClientRect();
        if (r.top < b.top + 60 || r.bottom > b.bottom - 20) { box.scrollTop += r.top - b.top - b.height / 3; }
      }
    }
    toTop.classList.toggle("show", window.scrollY > 600);
  }

  // ---------- Sprungziel (F1 / Umschalt+F1 öffnet die Datei mit #abschnitt) ----------

  function go(id, smooth) {
    var target = document.getElementById(id);
    if (!target) { return; }
    target.scrollIntoView({ behavior: smooth === false ? "auto" : "smooth", block: "start" });
    if (location.hash !== "#" + id) { history.replaceState(null, "", "#" + id); }
    target.classList.remove("flash"); void target.offsetWidth; target.classList.add("flash");
    document.body.classList.remove("nav-open");
  }

  tocRoot.addEventListener("click", function (ev) {
    var a = ev.target.closest("a[href^='#']");
    if (a) { ev.preventDefault(); go(a.getAttribute("href").substring(1)); }
  });
  window.addEventListener("hashchange", function () { go(location.hash.substring(1)); });

  // ---------- Bilder: in Originalgröße (1,5-fach gerendert), Klick = groß ----------

  var lightbox = document.createElement("div");
  lightbox.className = "lightbox";
  var big = document.createElement("img"); big.alt = "";
  lightbox.appendChild(big);
  document.body.appendChild(lightbox);
  lightbox.addEventListener("click", function () { lightbox.classList.remove("open"); });
  document.addEventListener("keydown", function (ev) { if (ev.key === "Escape") { lightbox.classList.remove("open"); } });

  content.querySelectorAll("figure img, img.shot").forEach(function (img) {
    function size() { if (img.naturalWidth) { img.style.width = Math.round(img.naturalWidth / 1.5) + "px"; } }
    if (img.complete) { size(); } else { img.addEventListener("load", size); }
    img.addEventListener("click", function () { big.src = img.src; lightbox.classList.add("open"); });
  });

  // ---------- Design ----------

  var themeBtn = document.getElementById("theme");
  var modes = ["auto", "light", "dark"];
  var mode = "auto";
  try { mode = localStorage.getItem("gsts-help-theme") || "auto"; } catch (e) { /* ohne Speicher */ }
  function applyTheme() {
    if (mode === "auto") { document.documentElement.removeAttribute("data-theme"); } else { document.documentElement.setAttribute("data-theme", mode); }
    themeBtn.title = text[mode];
    themeBtn.setAttribute("aria-label", text[mode]);
    themeBtn.textContent = mode === "dark" ? "☾" : mode === "light" ? "☀" : "◐";
  }
  themeBtn.addEventListener("click", function () {
    mode = modes[(modes.indexOf(mode) + 1) % modes.length];
    try { localStorage.setItem("gsts-help-theme", mode); } catch (e) { /* ohne Speicher */ }
    applyTheme();
  });
  applyTheme();

  // ---------- Menü (schmale Fenster), nach oben ----------

  document.getElementById("menu").addEventListener("click", function () { document.body.classList.toggle("nav-open"); });
  var toTop = document.createElement("button");
  toTop.className = "to-top"; toTop.textContent = "↑"; toTop.title = text.top; toTop.setAttribute("aria-label", text.top);
  toTop.addEventListener("click", function () { window.scrollTo({ top: 0, behavior: "smooth" }); });
  document.body.appendChild(toTop);

  window.addEventListener("scroll", markActive, { passive: true });
  window.addEventListener("load", function () {
    if (location.hash.length > 1) { go(location.hash.substring(1), false); }
    markActive();
  });
  markActive();
})();
