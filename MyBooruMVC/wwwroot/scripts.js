/*
LAYOUT
*/

function bindNavSearch(apihost) {
    const searchForm = document.getElementById("search-form");
    const searchInput = document.getElementById("search");
    const suggestions = document.getElementById("results");

    var thing = { length: -1, chars: "", data: [], isPerioded: true };

    searchInput.addEventListener("input", (e) => search(e, suggestions, retrieve, thing, apihost));
    searchForm.addEventListener("submit", () => formSubmit(searchInput));
    suggestions.addEventListener("click", (e) => submitEntry(e, searchInput, thing));
}
function makeUserButtons(apihost) {
    ajaxGet(apihost + "/api/user/getInfo",
        function (response) {
            $("<a href=\"/Gallery/upload\" id=\"button-upload\">Upload</a>").insertAfter("#button-home");
            $(".topnav-right").append("<a href='/user'> Hi there, <div id='username'>" + response.username + "</div></a>");
        },
        function (jqXHR) {
            $(".topnav-right").append("<a href='/user/login'>Sign in</a><a href='/user/register'>Sign up</a>");
        },
        true
    );
}
function ajaxGet(getUrl, successFunc, errorFunc, sendCreds, dataObject) {
    $.ajax({
        url: getUrl,
        method: "GET",
        type: "GET",
        data: dataObject,
        xhrFields: {
            withCredentials: sendCreds
        },
        success: function (response) {
            successFunc(response);
        },
        error: function (jqXHR) {
            if (errorFunc != null)
                errorFunc(jqXHR);
        }
    });
}
function formSubmit(input) {
    if (input.value.endsWith(","))
        input.value = input.value.slice(0, -1);
};
function submitEntry(e, input, obj) {
    const setValue = e.target.innerText;
    a = input.value;
    input.value = a.slice(0, a.lastIndexOf(",") + 1) + a.slice(a.lastIndexOf(",") + 1, a.length).replace(obj.chars, "");//yeah...
    input.value += setValue + (obj.isPerioded ? "," : "");
    obj.length = setValue.length;
    obj.chars = "";
    e.target.remove();
}
function getResults(input, retriever, obj, apihost) {
    const results = [];
    retriever(input, obj, apihost);
    if (obj.data == null)
        return;

    for (i = 0; i < obj.data.length; i++) {
        if (input.localeCompare(obj.data[i].slice(0, input.length), undefined, { sensitivity: 'accent' }) == 0)
            results.push(obj.data[i]);
    }
    return results;
}
function retrieve(str, obj, apihost) {
    if (str != null & str.length >= 2)
        ajaxGet(apihost + "/api/tag", x => obj.data = x, null, false, { tagname: str });
};
//deleteContentBackward, deleteContentForward, insertFromPaste, insertText
function search(e, suggest, retriever, obj, apihost) {
    let results = [];
    if (e.inputType == "insertText" || e.inputType == "insertFromPaste")
        obj.chars += e.data;
    if (e.inputType == "deleteContentBackward")
        obj.chars = obj.chars.slice(0, obj.chars.length - 1);

    const userInput = e.target.value;
    suggest.innerHTML = "";
    if (userInput.length > 0 || userInput.length > obj.length) {
        results = getResults(obj.chars, retriever, obj, apihost);

        if (results == null)
            return;

        suggest.style.display = "block";
        for (i = 0; i < results.length; i++) {
            suggest.innerHTML += "<li style='border-bottom: 1px solid white;' onclick=pickTag(this)>" + results[i] + "</li>";
        }
    }
}
function pickTag(liElement) {
    liElement.parentElement.innerHTML = "";
}
function checkAuth(jqXHR) {
    if (jqXHR.status == "401") {
        $(".modal-body").html("unauthorized!");
        $("#modal").addClass("active");
        return;
    };
    if (jqXHR.status == "403") {
        $(".modal-body").html("you don't have permissions for that!");
        $("#modal").addClass("active");
        return;
    };
}
function makeComment(apihost, item, printMediaID) {
    var date = new Date(item.timestamp * 1000).toLocaleString(navigator.language, { timezone: Intl.DateTimeFormat().resolvedOptions().timeZone });;
    var a = printMediaID
        ? $("<a></a>").attr("href", "/gallery/picture?id=" + item.mediaID).attr("id", "username").text("this picture").prepend(document.createTextNode("You said on "))
        : $("<a></a>").text(item.user).attr("href", window.location.origin + "/user/details?username=" + item.user).attr("id", "username");
    var p = $("<p></p>").text(item.text);
    var p2 = $("<p></p>").text(" said at: " + date).attr("class", "comm").attr("key", item.id).append("<br>");
    $("#comms").append(p2.prepend(a).prepend("<p class='close' title='Delete' role='button' onclick=deleteComment(this,'" + apihost + "')>&times;</p>").append(p));
}
function deleteComment(elem, apihost) {
    var commId = elem.parentElement.attributes['key'].value;
    $.ajax({
        url: apihost + "/api/comment/remove?id=" + commId,
        method: "DELETE",
        xhrFields: {
            withCredentials: true
        },
        success: function () {
            elem.parentElement.remove();
        },
        error: function (jqXHR) {
            checkAuth(jqXHR);
            alert(jQuery.parseJSON(jqXHR.responseText).result);
        }

    });
}
$(".close-button").on("click", function () {
    $("#modal").removeClass("active");
});

/*
PICTURE INDEX
*/

function populateGallery(apihost, page, reverse) {
    $.ajax({
        url: apihost + "/api/media",
        data: {
            page: page,
            reverse: reverse
        },
        success: function (response) {
            for (var i = 0; i < response.items.length; i++) {
                $("#smth").append("<a href='/gallery/picture?id=" + response.items[i].hash + "'>" + "<img src='" + apihost + "/" + response.items[i].thumb + "'>" + "</a>");
            }

            if (response.prevPage) {
                var currentPage = page;
                var prevPage = page - 1;
                $("#pages").append("<a href='/gallery?page=" + prevPage + "&reverse=" + reverse + "'>Previous</a>");
            }

            if (response.nextPage) {
                var currentPage = page;
                var nextPage = page + 1;
                $("#pages").append("<a href='/gallery?page=" + nextPage + "&reverse=" + reverse + "'>Next</a>");
            }
        }
    });
}

/*
PICTURE
*/

function bindTagSearch(apihost) {
    var tagThing = { length: -1, chars: "", data: [], isPerioded: false };

    const tagSearchForm = document.getElementById("tag-form");
    const tagSearchInput = document.getElementById("tags-to-add");
    const tagSuggestions = document.getElementById("tag-results");

    tagSearchInput.addEventListener("input", (e) => search(e, tagSuggestions, retrieve, tagThing, apihost));
    tagSearchForm.addEventListener("submit", () => tagFormSubmit(tagSearchInput));
    tagSuggestions.addEventListener("click", (e) => submitEntry(e, tagSearchInput, tagThing));
}

function tagFormSubmit(self) {
    self.value = "";
}
function getMediaDetails(apihost, mediaID) {
    $.ajax({
        url: apihost + "/api/media/details",
        data: {
            id: mediaID
        },
        success: function (response) {
            let tags = "";
            if (response.tags != null)
                response.tags.forEach(x => tags += x.name + " ");
            if (response.type.includes("image"))
                $("#base").append("<img src='" + apihost + "/" + response.path + "' alt='" + tags + "'>");
            if (response.type.includes("video"))
                $("#base").append("<video preload='metadata' loop controls autoplay muted src='" + apihost + "/" + response.path + "'>");

            makeList(response.tags);
            makeComments(apihost, response.comments);
        }
    });
}
function addComment(apihost, mediaID) {
    let commText = $("#new-comment").val();

    if (commText == null | commText === "") {
        $(".modal-body").html("comment cannot be empty");
        $("#modal").addClass("active");
        return;
    }

    $.ajax({
        url: apihost + "/api/comment/post",
        method: "POST",
        data: {
            hash: mediaID,
            commentText: commText
        },
        xhrFields: {
            withCredentials: true
        },
        success: function (response) {
            $("#new-comment").val("");
            ajaxGet(apihost + "/api/comment/byId", x => makeComment(apihost, x), null, false, { id: response });
        },
        error: function (jqXHR) {
            checkAuth(jqXHR);
        }
    });
}
function addTag(apihost, mediaID) {
    $.ajax({
        url: apihost + "/api/media/addTags",
        method: "GET",
        data: {
            id: mediaID,
            tags: $("#tags-to-add").val()
        },
        success: function (response) {
            makeList(response.items);
            $("#tags-to-add").val("");
        },
        xhrFields: {
            withCredentials: true
        },
        error: function (jqXHR) {
            checkAuth(jqXHR);
            alert("Can't add tag: " + jQuery.parseJSON(jqXHR.responseText).value.bad_tag + "\nTags should only contain letters and numbers\nAnd be 3 to 32 characters long");
        }
    });
}
function remove(apihost, mediaID) {
    $.ajax({
        url: apihost + "/api/media/remove?id=" + mediaID,
        method: "DELETE",
        xhrFields: {
            withCredentials: true
        },
        success: function () {
            location.replace("gallery");
        },
        error: function (jqXHR) {
            checkAuth(jqXHR);
            alert(jQuery.parseJSON(jqXHR.responseText).result);
        }
    })
}
function makeList(responseItems) {
    if (responseItems == null)
        return;

    for (var i = 0; i < responseItems.length; i++) {
        $("#tags").append("<a href='/gallery/search?tags=" + responseItems[i].name + "'>" + responseItems[i].name + "</a>");
    }
}
function makeComments(apihost, responseItems) {
    if (responseItems == null)
        return;

    for (var i = 0; i < responseItems.length; i++) {
        makeComment(apihost, responseItems[i]);
    }
}

/*
PICTURE SEARCH
*/

function populateSearch(apihost, tags, page, reverse) {
    $.ajax({
        url: apihost + "/api/media/byTag",
        method: "GET",
        data: {
            tags: tags,
            page: page,
            reverse: reverse
        },
        success: function (response) {
            if (response.items.length == 0) {
                $("#resultCount").text("0 results found")
                return;
            }

            $("#resultCount").text(response.count + " results found");

            for (var i = 0; i < response.items.length; i++) {
                $("#smth").append("<a href='/gallery/picture?id=" + response.items[i].hash + "'>" + "<img src='" + apihost + "/" + response.items[i].thumb + "'>" + "</a>");
            }

            if (response.prevPage) {
                var currentPage = page;
                var prevPage = page - 1;
                $("#pages").append("<a href='/gallery/search?tags=" + tags + "&page=" + prevPage + "&reverse=" + reverse + "'>Previous</a>")
            }

            if (response.nextPage) {
                var currentPage = page;
                var nextPage = page + 1;
                $("#pages").append("<a href='/gallery/search?tags=" + tags + "&page=" + prevPage + "&reverse=" + reverse + "'>Next</a>")
            }
        }
    });
}

/*
PICTURE UPLOAD
*/

function bindForms(apihost) {
    window.addEventListener('paste', e => {
        document.getElementById("file-input").files = e.clipboardData.files;
        if (!e.clipboardData.getData("text/plain") && e.clipboardData.files.length != 0) {
            $(".modal-body").html("File pasted successfully!");
            $("#modal").addClass("active");
        }
    });

    document.getElementById("file-form").addEventListener("submit", (e) => handleUpload(e, apihost));
    document.getElementById("uploadfrom-form").addEventListener("submit", (e) => handleUploadFrom(e, apihost));
}
function handleUpload(e, apihost) {
    e.preventDefault(); //important
    var formData = new FormData(e.target);

    $.ajax({
        url: apihost + "/api/media/upload",
        method: "POST",
        type: "POST",
        cache: false,
        data: formData,
        processData: false,
        contentType: false,
        xhrFields: {
            withCredentials: true
        },
        success: function (response) {

            e.target.reset();
            ajaxGet(apihost + "/api/media/details",
                function (uploadResponse) {
                    $(".modal-body").html("Uploaded:<br> <a href='/gallery/picture?id=" + uploadResponse.hash + "'>" + "<img src='" + apihost + "/" + uploadResponse.thumb + "'>" + "</a>");
                    $("#modal").addClass("active");
                },
                null,
                false,
                { id: response.value.item }
            );
        },
        error: function (jqXHR) {
            checkAuth(jqXHR);
            $(".modal-body").html(jQuery.parseJSON(jqXHR.responseText).value.item);
            $("#modal").addClass("active");
        }
    });
}
function handleUploadFrom(e, apihost) {
    e.preventDefault(); //important

    ajaxGet(
        apihost + "/api/media/uploadfrom",
        function (response) {

            e.target.reset();
            ajaxGet(apihost + "/api/media/details",
                function (uploadResponse) {
                    $(".modal-body").html("Uploaded:<br> <a href='/gallery/picture?id=" + uploadResponse.hash + "'>" + "<img src='" + apihost + "/" + uploadResponse.thumb + "'>" + "</a>");
                    $("#modal").addClass("active");
                },
                null,
                false,
                { id: response.value.item }
            );
        },
        function (jqXHR) {
            checkAuth(jqXHR);
            e.target.reset();
            $(".modal-body").html(jQuery.parseJSON(jqXHR.responseText).value.item);
            $("#modal").addClass("active");
        },
        true,
        { source: $("#link-input").val() }
    );
}

/*
USER HOMEPAGE
*/

function getLoggedUserDetails(apihost) {
    ajaxGet(apihost + "/api/user/getInfo",
        function (response) {
            $("#user-info").append("<p>" + response.username + "</p>");
            $("#user-info").append("<p>Registered: " + new Date(response.dateRegistered * 1000) + "</p>");
            $("#user-info").append("<p>User group: " + response.role + "</p>");
        },
        function (jqXHR) {
            checkAuth(jqXHR);
        },
        true
    );

    ajaxGet(apihost + "/api/user/getSessions",
        function (response) {
            response.allSessions.reverse();
            response.allSessions.forEach(function (item) {
                $("#base").append("<details id='" + item.id + "'><summary>Session " + item.id + "</summary><p>Last Active: " + new Date(item.lastActivity * 1000) + "Ip Address: " + item.ip + "UserAgent: " + item.userAgent + (item.isActiveSession ? "Active" : "<button onclick='closeSession(\"" + apihost + "\",\"" + item.id + "\");'>Close</button>") + "</p></details>");
            });
        },
        function (jqXHR) {
            checkAuth(jqXHR);
        },
        true
    );

    ajaxGet(apihost + "/api/comment/mine",
        function (response) {
            if (response.value.items != null)
                response.value.items.forEach(i => makeComment(apihost, i, true));
        },
        function (jqXHR) {
            checkAuth(jqXHR);
        },
        true
    );
}
function signOff(apihost) {
    ajaxGet(apihost + "/api/user/signoff", x => window.location.href = "/user/login", x => alert("Something went wrong, try again"), true, { fromAJAX: true });
}
function closeSession(apihost, session) {
    ajaxGet(apihost + "/api/user/closeSession", x => $("#" + session + "").remove(), x => alert("Something went wrong!"), true, { sessionId: session });
}

/*
USER LOGIN
*/

function bindLogin(apihost) {
    document.getElementById("login-form").addEventListener("submit", (e) => processLogin(e, apihost));
}
function processLogin(e, apihost) {
    e.preventDefault();
    var formData = new FormData(e.target);

    $.ajax({
        url: apihost + "/api/user/signin",
        method: "POST",
        type: "POST",
        cache: false,
        data: formData,
        processData: false,
        contentType: false,
        xhrFields: {
            withCredentials: true
        },
        success: function (response) {
            console.log(response);
            window.location = "/gallery";
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.log(`$first: ${jqXHR.responseText} second: ${textStatus} third: ${errorThrown}`);
        }
    });
}

/*
USER REGISTER
 */

function bindRegister(apihost) {
    document.getElementById("register-form").addEventListener("submit", (e) => processRegister(e, apihost));
}
function processRegister(e, apihost) {
    e.preventDefault();
    var formData = new FormData(e.target);

    $.ajax({
        url: apihost + "/api/user/signup",
        method: "POST",
        type: "POST",
        cache: false,
        data: formData,
        processData: false,
        contentType: false,
        xhrFields: {
            withCredentials: true
        },
        success: function (response) {
            console.log(response);
            window.location = "/gallery";
        },
        error: function (jqXHR, textStatus, errorThrown) {
            console.log(`$first: ${jqXHR.responseText} second: ${textStatus} third: ${errorThrown}`);
        }
    });
}