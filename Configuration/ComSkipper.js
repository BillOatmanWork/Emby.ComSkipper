define(["loading", "dialogHelper", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper) {

        var pluginId = "1024CC72-802F-4EFB-89FB-F190AFF2A42E";

        return function(view) {
            view.addEventListener('viewshow',
                async () => {
                    var chkEnableAutoSkip = view.querySelector('#autoSkipCommercials');

                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        chkEnableAutoSkip.checked = config.EnableComSkipper ?? false;
                    });

                    chkEnableAutoSkip.addEventListener('change', (elem) => {
                        elem.preventDefault();
                        var autoSkip = chkEnableAutoSkip.checked;
                        enableAutoSkip(autoSkip);
                    });

                    function enableAutoSkip(autoSkip) {
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableComSkipper = autoSkip;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => {});
                        });
                    }
                });
        }
    });