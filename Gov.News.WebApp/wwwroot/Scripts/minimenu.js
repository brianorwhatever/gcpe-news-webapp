
function closeMiniMenu(speed) {
    $('.mini-menu-trigger').removeClass('triggered');
    var miniMenu = $('#mini-menu');
    if (speed == undefined) {
        miniMenu.hide();
    } else {
        miniMenu.slideUp('fast');
    }
}

function closeSearchMenu() {
    $('.mini-menu-search').slideUp('fast');
    $('.mini-search-trigger').removeClass('triggered');
}

$(function () {
    $('.mini-menu-trigger').on('click', function () {
        closeSearchMenu();
        var menuTrigger = $('.mini-menu-trigger');
        var opened = menuTrigger.hasClass('triggered');
        var miniMenu = $('#mini-menu');
        if (opened) {
            closeMiniMenu('slow');
        } else {
            menuTrigger.addClass('triggered');
            miniMenu.slideDown(300, function () { });
        }
    });

    $('.mini-search-trigger').on('click', function () {
        closeMiniMenu();
        var menuTrigger = $('.mini-search-trigger');
        var opened = menuTrigger.hasClass('triggered');
        var miniMenu = $('.mini-menu-search');
        if (opened) {
            closeSearchMenu();
        } else {
            menuTrigger.addClass('triggered');
            miniMenu.slideDown(300, function () { });
        }
    });

    $(".level-trigger").on('click', function () {
        var childList = $(this).siblings().parent().find('ul:first');
        if (childList.length === 0)
            return;
        if (childList.is(':visible')) {
            childList.slideUp(400, function () { });
            $(this).removeClass('open');
            childList.find('ul').removeClass('open');
        } else {
            childList.slideDown(400, function () { });
            $(this).addClass('open');
        }
    });
});
