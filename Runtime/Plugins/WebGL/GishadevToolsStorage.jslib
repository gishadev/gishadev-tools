// Browser-side backing for gishadev.tools SavingSystem.
//
// Ships with the package — no per-project copy needed, unlike the WebGL template. Every exported
// symbol is prefixed, because Emscripten symbols are global across the whole build and an unprefixed
// name like _Save would collide at link time with any other plugin defining it.

var GishadevToolsStoragePlugin = {

  $GishadevToolsStorage: {
    // Namespaces our entries so ClearAll can't wipe keys belonging to another game on the same origin.
    PREFIX: "gdt_",

    scoped: function (key) {
      return GishadevToolsStorage.PREFIX + key;
    },

    // localStorage throws on access when cookies are blocked or in some private-browsing modes,
    // so it's never touched outside a try/catch.
    available: function () {
      try {
        return typeof localStorage !== "undefined" && localStorage !== null;
      } catch (e) {
        return false;
      }
    },

    warn: function (message) {
      console.warn("[gishadev.tools] " + message);
    },

    toPointer: function (str) {
      if (str === null || str === undefined) return 0;
      var size = lengthBytesUTF8(str) + 1;
      var buffer = _malloc(size);
      stringToUTF8(str, buffer, size);
      return buffer;
    }
  },

  GishadevTools_Save__deps: ["$GishadevToolsStorage"],
  GishadevTools_Save: function (keyPtr, valuePtr) {
    if (!GishadevToolsStorage.available()) {
      GishadevToolsStorage.warn("localStorage is unavailable, save dropped.");
      return;
    }

    var key = GishadevToolsStorage.scoped(UTF8ToString(keyPtr));
    try {
      localStorage.setItem(key, UTF8ToString(valuePtr));
    } catch (e) {
      // Usually QuotaExceededError: localStorage is capped around 5 MB per origin.
      GishadevToolsStorage.warn("Failed to save \"" + key + "\": " + e +
        " (localStorage is limited to about 5MB — consider FileSaverSystem for larger saves).");
    }
  },

  GishadevTools_Load__deps: ["$GishadevToolsStorage"],
  GishadevTools_Load: function (keyPtr) {
    if (!GishadevToolsStorage.available()) return 0;

    try {
      // getItem returns null for a missing key, which marshals back to a null string in C#.
      return GishadevToolsStorage.toPointer(
        localStorage.getItem(GishadevToolsStorage.scoped(UTF8ToString(keyPtr))));
    } catch (e) {
      GishadevToolsStorage.warn("Failed to load: " + e);
      return 0;
    }
  },

  GishadevTools_HasKey__deps: ["$GishadevToolsStorage"],
  GishadevTools_HasKey: function (keyPtr) {
    if (!GishadevToolsStorage.available()) return 0;

    try {
      return localStorage.getItem(GishadevToolsStorage.scoped(UTF8ToString(keyPtr))) !== null ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  GishadevTools_Delete__deps: ["$GishadevToolsStorage"],
  GishadevTools_Delete: function (keyPtr) {
    if (!GishadevToolsStorage.available()) return;

    try {
      localStorage.removeItem(GishadevToolsStorage.scoped(UTF8ToString(keyPtr)));
    } catch (e) {
      GishadevToolsStorage.warn("Failed to delete: " + e);
    }
  },

  GishadevTools_ClearAll__deps: ["$GishadevToolsStorage"],
  GishadevTools_ClearAll: function () {
    if (!GishadevToolsStorage.available()) return;

    try {
      // Collect first: removing while iterating shifts the remaining indices.
      var doomed = [];
      for (var i = 0; i < localStorage.length; i++) {
        var key = localStorage.key(i);
        if (key !== null && key.indexOf(GishadevToolsStorage.PREFIX) === 0) doomed.push(key);
      }

      for (var j = 0; j < doomed.length; j++) localStorage.removeItem(doomed[j]);
    } catch (e) {
      GishadevToolsStorage.warn("Failed to clear: " + e);
    }
  },

  GishadevTools_SyncFs__deps: ["$GishadevToolsStorage"],
  GishadevTools_SyncFs: function () {
    // Files written under persistentDataPath live in an in-memory filesystem until this runs;
    // without it they're gone on the next page load. false = flush memory to IndexedDB.
    try {
      FS.syncfs(false, function (err) {
        if (err) GishadevToolsStorage.warn("Filesystem sync failed: " + err);
      });
    } catch (e) {
      GishadevToolsStorage.warn("Filesystem sync threw: " + e);
    }
  }
};

autoAddDeps(GishadevToolsStoragePlugin, "$GishadevToolsStorage");
mergeInto(LibraryManager.library, GishadevToolsStoragePlugin);
