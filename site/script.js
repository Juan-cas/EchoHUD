// SPDX-License-Identifier: GPL-3.0-only
//
// The two demo timers on the page. They imitate what Echo HUD's timers look
// like: click to start a ten-second countdown, the last three seconds turn
// amber, then the timer counts up ("+0:04") to show how long it has been
// ready. Clicking again restarts it. Nothing here talks to any server.

(function () {
  "use strict";

  var SECONDS = 10;
  var RING_LENGTH = 2 * Math.PI * 46; // circumference of the SVG circle (r = 46)

  function format(seconds) {
    var whole = Math.max(0, Math.floor(seconds));
    var rest = whole % 60;
    return Math.floor(whole / 60) + ":" + (rest < 10 ? "0" : "") + rest;
  }

  /**
   * Wires up one timer.
   *   button  the clickable element
   *   time    element showing the time
   *   name    element showing the name (shows "Ready" while counting up)
   *   colour  the timer's normal colour
   *   draw    function(fractionLeft, colour) that updates the ring or bar
   */
  function timer(button, time, name, colour, draw) {
    var label = name.textContent;
    var endsAt = 0;
    var handle = 0;

    function tick() {
      var left = (endsAt - Date.now()) / 1000;
      if (left > 0) {
        time.textContent = format(Math.ceil(left));
        draw(left / SECONDS, left <= 3 ? "#ffb020" : colour);
        return;
      }
      button.classList.remove("is-running");
      button.classList.add("is-done");
      time.textContent = "+" + format(-left);
      name.textContent = "Ready";
      draw(1, colour);
      if (-left > 60) stop(); // do not run forever on a forgotten tab
    }

    function stop() {
      window.clearInterval(handle);
      button.classList.remove("is-running", "is-done");
      time.textContent = format(SECONDS);
      name.textContent = label;
      draw(0, colour);
    }

    button.addEventListener("click", function () {
      window.clearInterval(handle);
      endsAt = Date.now() + SECONDS * 1000;
      name.textContent = label;
      button.classList.remove("is-done");
      button.classList.add("is-running");
      tick();
      handle = window.setInterval(tick, 200);
      var hint = document.getElementById("timer-hint");
      if (hint) hint.textContent = "Ten seconds for the demo. Real timers run from one second to a day.";
    });
  }

  var arc = document.getElementById("ring-arc");
  timer(document.getElementById("ring"), document.getElementById("ring-time"), document.getElementById("ring-name"), "#2dd4bf",
    function (fraction, colour) {
      arc.style.strokeDashoffset = String(RING_LENGTH * (1 - fraction));
      arc.style.stroke = colour;
    });

  var fill = document.getElementById("bar-fill");
  timer(document.getElementById("bar"), document.getElementById("bar-time"), document.getElementById("bar-name"), "#60a5fa",
    function (fraction, colour) {
      fill.style.width = (fraction * 100) + "%";
      fill.style.background = colour;
    });
})();
