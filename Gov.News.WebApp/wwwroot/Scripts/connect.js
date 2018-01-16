function hookSectionsExpandCollapse() {
    $('.expandable-section .expand').on('click', function (event) {
        $(this).closest('.expandable-section').removeClass('collapsed').addClass('expanded');
        event.preventDefault();
    });
    $('.expandable-section .collapse').on('click', function (event) {
        $(this).closest('.expandable-section').removeClass('expanded').addClass('collapsed');
        event.preventDefault();
    });
    $(window).bind('hashchange', function (e) {
        expandByHash();
    });
    //Call on load
    expandByHash();
}
function expandByHash() {
    var hash = window.location.hash;
    if (hash.length != 0) {
        $(".expandable-section.collapsed." + hash.substr(1) + " a.expand").click();
    }
}
function ValidateForm(messageText) {
    var anyMinistriesSelected = $("input[type=checkbox][value!=true]:checked:first.ministries-checkbox");
    var newsDelivering = $("[name^='News'][type=checkbox][value=true]");
    if (anyMinistriesSelected.length > 0 && newsDelivering.length && newsDelivering.filter(":checked:first").length == 0) {
        $(messageText).text("Please select a delivery option.");
        return false;
    }
    var emailAddress = $("input[name='EmailAddress']");
    if (emailAddress.val().indexOf("@") == -1) {
        $(messageText).text("Please enter a valid email address.");
        return false;
    }
    else if ($("input[type=checkbox][value!=true]:checked:first").length == 0) {
        $(messageText).text("Please select one or more categories.");
        return false;
    }
    return true;
}
function hookSubscribeToAllNewsOnDemand() {
    var newsOnDemandCustomOptions = $("#newsOnDemandCustomOptions");
    var allNODCheckboxes = $(newsOnDemandCustomOptions).find("input[type=checkbox][value!=true]");
    var allNewsRadio = $("input[name='AllNews']");
    if (allNODCheckboxes.filter(":not(:checked):first").length) {
        allNewsRadio.filter("[value=false]").prop('checked', 'checked');
    }
    else {
        allNewsRadio.filter("[value=true]").prop('checked', 'checked');
    }
    allNewsRadio.click(function () {
        var value = $(this).val();
        if (value === 'false') {
            // uncheck all boxes
            $(newsOnDemandCustomOptions).find("input[type=checkbox][value!=true]:checked").removeAttr('checked');
        }
        else {
            // check all boxes
            $(newsOnDemandCustomOptions).find("input[type=checkbox][value!=true]:not(:checked)").prop('checked', true);
        }
    });
    allNODCheckboxes.click(function () {
        if (!$(this).prop('checked')) {
            allNewsRadio.filter("[value=false]").prop('checked', 'checked');
        }
    });
}
