mergeInto(LibraryManager.library, {
  ReflectableFlushPersistentData: function () {
    if (typeof FS === "undefined" || !FS.syncfs) return;
    var moduleState = (typeof Module !== "undefined") ? Module : null;
    if (moduleState && moduleState.reflectableSyncInProgress) {
      moduleState.reflectableSyncAgain = true;
      return;
    }
    function syncPendingWrites() {
      if (moduleState) moduleState.reflectableSyncInProgress = true;
      FS.syncfs(false, function (error) {
        if (error) console.error("Reflectable: IndexedDB save flush failed", error);
        if (!moduleState) return;
        moduleState.reflectableSyncInProgress = false;
        if (moduleState.reflectableSyncAgain) {
          moduleState.reflectableSyncAgain = false;
          syncPendingWrites();
        }
      });
    }
    syncPendingWrites();
  }
});
