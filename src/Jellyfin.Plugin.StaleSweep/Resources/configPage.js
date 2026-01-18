(function () {
  'use strict';

  var pluginId = '6e4050cf1ce94f9daed1cc36cbe5d2ef';

  function getLibraries() {
    return ApiClient.getJSON(ApiClient.getUrl('StaleSweep/Libraries'));
  }

  function loadConfig() {
    return ApiClient.getPluginConfiguration(pluginId);
  }

  function setStatus(message) {
    if (window.Dashboard && Dashboard.showToast) {
      Dashboard.showToast(message);
      return;
    }
    console.log(message);
  }

  function renderLibraries(container, libraries, selected) {
    container.innerHTML = '';

    libraries.forEach(function (lib) {
      var id = (lib.Id || '').toLowerCase();
      var isChecked = selected.indexOf(id) !== -1;

      var div = document.createElement('div');
      div.className = 'checkboxContainer';

      var label = document.createElement('label');
      var checkbox = document.createElement('input');
      checkbox.type = 'checkbox';
      checkbox.setAttribute('is', 'emby-checkbox');
      checkbox.setAttribute('data-libraryid', id);
      checkbox.checked = isChecked;

      var span = document.createElement('span');
      span.textContent = lib.Name || id;

      label.appendChild(checkbox);
      label.appendChild(span);
      div.appendChild(label);
      container.appendChild(div);
    });
  }

  function normalizeGuidN(guid) {
    return (guid || '').replace(/[^0-9a-f]/gi, '').toLowerCase();
  }

  function getSelectedLibraryIds(container) {
    var inputs = container.querySelectorAll('input[type="checkbox"][data-libraryid]');
    var out = [];
    for (var i = 0; i < inputs.length; i++) {
      if (inputs[i].checked) {
        out.push(normalizeGuidN(inputs[i].getAttribute('data-libraryid')));
      }
    }
    return out;
  }

  function wirePage() {
    var form = document.getElementById('staleSweepConfigForm');
    var librariesContainer = document.getElementById('libraries');
    if (!form || form.getAttribute('data-wired') === '1') {
      return;
    }
    form.setAttribute('data-wired', '1');

    function refresh() {
      return Promise.all([loadConfig(), getLibraries()]).then(function (results) {
        var config = results[0] || {};
        var libraries = results[1] || [];

        document.getElementById('ageLimitDays').value = config.AgeLimitDays || 365;
        document.getElementById('dryRun').checked = config.DryRun !== false;
        document.getElementById('tvMode').value = String(config.TvMode || 0);

        var selected = (config.LibraryIds || []).map(normalizeGuidN);
        renderLibraries(librariesContainer, libraries, selected);
      });
    }

    form.addEventListener('submit', function (e) {
      e.preventDefault();

      loadConfig().then(function (config) {
        config.AgeLimitDays = parseInt(document.getElementById('ageLimitDays').value || '365', 10);
        config.DryRun = !!document.getElementById('dryRun').checked;
        config.TvMode = parseInt(document.getElementById('tvMode').value || '0', 10);
        config.LibraryIds = getSelectedLibraryIds(librariesContainer);

        ApiClient.updatePluginConfiguration(pluginId, config).then(function () {
          setStatus('Stale Sweep settings saved');
        });
      });
    });

    refresh().catch(function (err) {
      console.error(err);
      setStatus('Failed to load Stale Sweep settings');
    });
  }

  document.addEventListener('viewshow', function (e) {
    var page = e.target;
    if (page && page.id === 'staleSweepConfigPage') {
      wirePage();
    }
  });
})();
