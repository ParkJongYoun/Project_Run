mergeInto(LibraryManager.library, {
  ProjectRunSyncSave: function () {
    if (typeof FS === 'undefined') return;
    if (Module.projectRunSyncBusy) { Module.projectRunSyncAgain = true; return; }
    Module.projectRunSyncBusy = true;
    var flush = function () {
      FS.syncfs(false, function (error) {
        if (error) console.warn('Project Run browser save:', error);
        if (Module.projectRunSyncAgain) { Module.projectRunSyncAgain = false; flush(); }
        else Module.projectRunSyncBusy = false;
      });
    };
    flush();
  }
});
