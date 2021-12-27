define(["loading", "dialogHelper", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper) {

        var pluginId = "1024CC72-802F-4EFB-89FB-F190AFF2A42E";

        return function(view) {
            view.addEventListener('viewshow',
                async () => {
                    var chkEnableAutoSkip = view.querySelector('#autoSkipCommercials');
                    var chkDisableMessage = view.querySelector('#disableMessage');

                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        chkEnableAutoSkip.checked = config.EnableComSkipper ?? false;
                        chkDisableMessage.checked = config.DisableMessage ?? false;
                    });

                    chkEnableAutoSkip.addEventListener('change', (elem) => {
                        elem.preventDefault();
                        var autoSkip = chkEnableAutoSkip.checked;
                        enableAutoSkip(autoSkip);
                    });

                    chkDisableMessage.addEventListener('change', (elem) => {
                        elem.preventDefault();
                        var disMsg = chkDisableMessage.checked;
                        enableDisableMessage(disMsg);
                    });

                    function enableAutoSkip(autoSkip) {
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableComSkipper = autoSkip;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => {});
                        });
                    }

                    function enableDisableMessage(disMsg) {
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.DisableMessage = disMsg;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                        });
                    }
                });
        }
    });