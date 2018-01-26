var facebookLoggedInCallback = null;
var facebookInitialized = null;
var uid;
var accessToken;

function facebookShare(link, title) {
    FB.ui({
        method: 'feed',
        link: link,
        caption: title
    }, function(response) {
        console.log(response);
    });
}

function facebookComment(objectId, message) {
    FB.api("/" + objectId + "/comments",
        "POST", {
            "message" : message
        },
        function (response) {
            console.log(response);
        });

    window.hideFacebookComment();
}

function facebookLike(objectId, like) {
    FB.api("/" + objectId + "/likes",
        like,
        function(response) {
            console.log(response);
        });
}

$(function() {

    function loginToFacebook() {
        FB.login(function(response) {
            if (response.status === 'connected') {
                // Logged into your app and Facebook.
                uid = response.authResponse.userID;
                accessToken = response.authResponse.accessToken;
                facebookLoggedInCallback();
            }
            facebookLoggedInCallback = null;
        }, { scope: 'publish_actions' });
    }

    window.fbAsyncInit = function() {
        FB.init({
            appId: '1024918554246018',
            xfbml: false,
            version: 'v2.10',
            status: true
        });

        FB.getLoginStatus(function(response) {
            if (response.status === 'connected') {
                // the user is logged in and has authenticated your
                // app, and response.authResponse supplies
                // the user's ID, a valid access token, a signed
                // request, and the time the access token 
                // and signed request each expire
                uid = response.authResponse.userID;
                accessToken = response.authResponse.accessToken;
                facebookLoggedInCallback();
                facebookLoggedInCallback = null;
                facebookInitialized = true;
            } else if (facebookLoggedInCallback) {
                loginToFacebook();
            }
            facebookInitialized = true;
        });
    }

    function facebookAPIAuthCall(loggedInCallback) {
        if (accessToken) {
            loggedInCallback();
            return;
        }
        facebookLoggedInCallback = loggedInCallback;
        if (facebookInitialized) {
            loginToFacebook();
            return;
        }
        initializeFacebook();
    }

    function initializeFacebook() {
        if (facebookInitialized == 0)
            return; // initialization in progress

        facebookInitialized = 0;

        (function(d, s, id) {
            var js, fjs = d.getElementsByTagName(s)[0];
            if (d.getElementById(id))
                return;
            js = d.createElement(s);
            js.id = id;
            js.src = "//connect.facebook.net/en_CA/sdk.js";
            fjs.parentNode.insertBefore(js, fjs);
        }(document, 'script', 'facebook-jssdk'));
    }

    /*$(".facebook-share").on('click', function(e) {
        e.preventDefault();
        var link = $(this).data('url');
        var title = $(this).data('title');
        facebookAPIAuthCall(function () { facebookShare(link, title); });
    });*/

    function toggleLike(item) {
        item.toggleClass("facebook-unlike");
        item.toggleClass("facebook-like");

        if (item.hasClass("facebook-like")) {
            item.text("Like");
        } else {
            item.text("Unlike");
        }
    }

    //$(".facebook-like").on('click', function(e) {
    //    e.preventDefault();
    //    var like = "POST";
    //    if ($(this).hasClass("facebook-unlike")) {
    //        like = "DELETE";
    //    }
    //    var objectId = $(this).data('facebook-object-id');
    //    facebookAPIAuthCall(function () { facebookLike(objectId, like); });
    //    toggleLike($(this));
    //});

    $(".facebook-comment-trigger").on('click', function (e) {
        e.preventDefault();
        var objectId = $(this).data('facebook-object-id');
        var message = $("#facebook-comment-message-" + $(this).data('sliderId')).val();
        facebookAPIAuthCall(function() {
            facebookComment(objectId, message);
    });
    });
});