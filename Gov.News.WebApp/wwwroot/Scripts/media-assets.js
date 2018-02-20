var mediaAssetProxyUrl = null;

// initializes the embedded media assets
function initializeEmbeddedMediaPlaceholders(proxyUrl) {
    if (proxyUrl != null) {
        mediaAssetProxyUrl = proxyUrl;
    }
    var assets = $(".asset.video, .asset.audio");
    for (j = 0; j < assets.length; j++) {
        var asset = $(assets[j]);
        var placeholder = asset.find(".placeholder-container");
        var placeholderImage = $("#placeholder-image");
        if (placeholderImage.length > 0) {
            var assetPadding = 0;
            try {
                assetPadding = parseFloat($(asset).css("padding-bottom"))
            }
            catch (e) { }
            // NOTE: the get(0).height is to access an item that is not visible in the browser.
            //       In Chrome this is works generally but we did see some failures during testing.
            var placeholderImageHeight = $("#placeholder-image").get(0).height - assetPadding;
            $(asset).height(placeholderImageHeight);
        }
        var placeholderContainerHeight = $(placeholder).height();
        if (asset.hasClass("wowza")) {
            placeholderContainerHeight = $(placeholder).find("img").height();
        }
        var instructionsHeight = $(placeholder).find(".overlay-container .outer .inner").height();
        var offset = parseInt((placeholderContainerHeight - instructionsHeight) / 2);
        $(placeholder).find(".overlay-container .outer .inner").css("top", offset + "px").css("visibility", "visible");
        if ((asset.data("media-type") != undefined) && (asset.data("media-type") != "")) {
            mediaType = asset.data("media-type");
            if (getMediaAssetPreference(mediaType) == 1) {
                createMediaEmbed(asset, true);
            }
        }
    }
}

// resize embedded media assets
function resizeEmbeddedMediaAssets() {
    var assets = $(".asset.video, .asset.audio");
    for (j = 0; j < assets.length; j++) {
        var asset = $(assets[j]);
        var placeholder = asset.find(".placeholder-container");
        var placeholderContainerHeight = $(placeholder).height();

        var assetPadding = 0;
        try {
            assetPadding = parseFloat($(asset).css("padding-bottom"))
        }
        catch (e) { }

        $(asset).height($("#placeholder-image").get(0).height - assetPadding);

        if (asset.hasClass("wowza")) {
            placeholderContainerHeight = $(placeholder).find("img").height();
        }

        var instructionsHeight = $(placeholder).find(".overlay-container .outer .inner").height();
        var offset = parseInt((placeholderContainerHeight - instructionsHeight) / 2);
        $(placeholder).find(".overlay-container .outer .inner").css("top", offset + "px").css("visibility", "visible");
    }
}

// plays the embedded media asset
function playMedia(playButton, persistPreference) {
    if (persistPreference) {
        playButton = $(playButton).parents(".inner").find(".play-button");
    }
    var innerContainer = $(playButton).parent(".inner");
    if ($(innerContainer).find(".play-instructions").css("display") != "block") {
        var playButton = $(innerContainer).find(".play-button");
        var playInstructions = $(innerContainer).find(".play-instructions");
        $(function () {
            try {
                innerContainer.switchClass("not-expanded", "expanded", 350, "easeInOutQuad");
                playInstructions.fadeIn(350);
            }
            catch (e) {
                innerContainer.toggleClass("not-expanded");
                innerContainer.toggleClass("expanded");
                playInstructions.fadeIn(350);
            }
        });
    }
    else {
        var embeddedMedia = $(playButton).parents("div.asset");
        createMediaEmbed(embeddedMedia, false);
    }
    if (persistPreference) {
        var embeddedMedia = $(playButton).parents("div.asset");
        var mediaType = "";
        if ((embeddedMedia.data("media-type") != undefined) && (embeddedMedia.data("media-type") != "")) {
            mediaType = embeddedMedia.data("media-type");
            if (mediaType != "") {
                saveMediaAssetPreference(mediaType);
            }
            var assets = $(".asset");
            for (i = 0; i < assets.length; i++) {
                var asset = $(assets[i]);
                if ((asset.data("media-type") != undefined) && (asset.data("media-type") == mediaType)) {
                    createMediaEmbed(asset, true);
                }
            }
        }
    }
}

// creates the embedded media player
function createMediaEmbed(embeddedMedia, display) {
    if (embeddedMedia.find("#videoPlayer").length == 1) {
        var videoPlayer = embeddedMedia.find("#videoPlayer");
        var placeholderContainer = embeddedMedia.find(".placeholder-container");
        if (display) {
            videoPlayer.css("display", "block");
            placeholderContainer.css("display", "none");
        }
        else {
            videoPlayer.css("display", "block");
            placeholderContainer.fadeOut(250);
        }
        if (secondaryIsLiveCallback != undefined) {
            secondaryIsLiveCallback(true, playlist);
        }
    }
    else {
        var mediaPlaceholderContainer = embeddedMedia.find(".media-player-container");
        if (mediaPlaceholderContainer.find("iframe").length == 0) {
            var placeholderContainer = embeddedMedia.find(".placeholder-container");
            var mediaType = "";
            var mediaId = "";
            var mediaUrl = "";
            var embedHtml = "";
            if ((embeddedMedia.data("media-type") != undefined) && (embeddedMedia.data("media-type") != "")) {
                mediaType = embeddedMedia.data("media-type");
                mediaId = embeddedMedia.data("media-id");
                switch (mediaType) {
                    case "youtube":
                        mediaUrl = mediaAssetProxyUrl + "youtube?id=" + mediaId;
                        break;
                    case "soundcloud":
                        mediaUrl = mediaAssetProxyUrl + "soundcloud?id=" + mediaId;
                        break;
                    case "facebook":
                        mediaUrl = mediaAssetProxyUrl + "facebook?id=" + mediaId;
                        break;
                }
                if (!(display)) {
                    mediaUrl += "&amp;autoPlay=true";
                }
                embedHtml = $("<iframe src='" + mediaUrl + "' frameborder='0' scrolling='no' allowfullscreen wmode='Opaque' width='100%' height='100%'></iframe><div class='clear'></div>");
            }
            if (embedHtml != "") {
                mediaPlaceholderContainer.append(embedHtml);
                mediaPlaceholderContainer.css("display", "block");
                if (display) {
                    placeholderContainer.css("display", "none");
                }
                else {
                    placeholderContainer.fadeOut(250);
                }
            }
        }
    }
}

// close the instructions pop-up
function closeInstructions(closeButton) {
    var innerContainer = $(closeButton).parents(".inner");
    var asset = innerContainer.parents(".asset");
    var playButton = $(innerContainer).find(".play-button");
    var playInstructions = $(innerContainer).find(".play-instructions");
    $(function () {
        playInstructions.fadeOut(100, function () {
            try {
                innerContainer.switchClass("expanded", "not-expanded", 200, "easeInOutQuad");
            }
            catch (e) {
                innerContainer.toggleClass("expanded");
                innerContainer.toggleClass("not-expanded");
            }
        })
    })
}

// returns the media asset preferences as an array
function returnMediaAssetCookie() {
    var name = "MediaAssetPreferences=";
    var cookieArray = document.cookie.split(";");
    for (var i = 0; i < cookieArray.length; i++) {
        var cookie = cookieArray[i].trim();
        if (cookie.indexOf(name) == 0) {
            return JSON.parse(cookie.substring(name.length, cookie.length));
        }
    }

    return [{ "name": "facebook", "enabled": 0 }, { "name": "soundcloud", "enabled": 0 }, { "name": "youtube", "enabled": 0 }, { "name": "wowza", "enabled": 0 }];
}

// saves the media asset preferences cookies
function saveMediaAssetPreference(mediaType) {
    var mediaAssetPreferences = returnMediaAssetCookie();
    for (i = 0; i < mediaAssetPreferences.length; i++) {
        if (mediaAssetPreferences[i].name == mediaType) {
            mediaAssetPreferences[i].enabled = 1;
            break;
        }
    }
    var expiryDays = 365;
    var date = new Date();
    date.setTime(date.getTime() + (expiryDays * 24 * 60 * 60 * 1000));
    var expires = "expires=" + date.toUTCString();
    document.cookie = "MediaAssetPreferences=" + JSON.stringify(mediaAssetPreferences) + "; " + expires + "; path=/;";
}

// returns the "enabled" property associated with a particular media asset preference
function getMediaAssetPreference(mediaType) {
    var mediaAssetPreferences = returnMediaAssetCookie();
    for (i = 0; i < mediaAssetPreferences.length; i++) {
        if (mediaAssetPreferences[i].name == mediaType) {
            return mediaAssetPreferences[i].enabled;
        }
    }
    return 0;
}