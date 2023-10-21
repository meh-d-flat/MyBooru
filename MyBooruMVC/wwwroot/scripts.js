//LAYOUT
function bindNavSearch(apihost) {
    var thing = { length: -1, chars: "", data: [], isPerioded: true };
    const searchForm = document.getElementById("search-form");
    const searchInput = document.getElementById("search");
    const suggestions = document.getElementById("results");
    searchInput.addEventListener("input", (e) => search(e, suggestions, retrieve, thing, apihost));
    searchForm.addEventListener("submit", () => formSubmit(searchInput));
    suggestions.addEventListener("click", (e) => submitEntry(e, searchInput, thing));
}
function makeUserButtonsAndModal(apihost) {
    ajaxNonPost(apihost + "/api/user/getInfo", "GET",
        function (response) {
            $("<a href=\"/Gallery/upload\" id=\"button-upload\">Upload</a>").insertAfter("#button-home");
            $(".topnav-right").append("<a href='/user'> Hi there, <div id='username'>" + response.username + "</div></a>");
        },
        function (jqXHR) {
            $(".topnav-right").append("<a href='/user/login'>Sign in</a><a href='/user/register'>Sign up</a>");
        }, true);
        $(".close-button").on("click", function () {
            $("#modal").removeClass("active");
        });
}
function ajaxNonPost(getUrl, method, successFunc, errorFunc, sendCreds, dataObject) {
    $.ajax({
        url: getUrl,
        method: method,
        data: dataObject,
        headers: { 'x-query': this.location.search },
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
function ajaxPost(postUrl, succesF, errorF, sendCreds, formData) {
    $.ajax({
        url: postUrl,
        method: "POST",
        cache: false,
        data: formData,
        processData: false,
        contentType: false,
        xhrFields: { withCredentials: sendCreds },
        headers: { 'x-query': this.location.search },
        success: res => succesF(res),
        error: (jqXHR, textStatus, errorThrown) => {
            if (errorF != null)
                errorF(jqXHR, textStatus, errorThrown);
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
        ajaxNonPost(apihost + "/api/tag", "GET", x => obj.data = x, null, false, { tagname: str });
};
function search(e, suggest, retriever, obj, apihost) {//deleteContentBackward, deleteContentForward, insertFromPaste, insertText
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
    ajaxNonPost(apihost + "/api/comment/remove?id=" + commId, "DELETE",
        () => elem.parentElement.remove(),
        x => {
            checkAuth(x);
            alert(jQuery.parseJSON(x.responseText).result);
        }, true, null);
}
//PICTURE INDEX
function populateGallery(apihost, page, reverse) {
    ajaxNonPost(apihost + "/api/media", "GET",
        x => {
            for (var i = 0; i < x.items.length; i++) {
                $("#smth").append("<a href='/gallery/picture?id=" + x.items[i].hash + "'>" + "<img src='" + apihost + "/" + x.items[i].thumb + "'>" + "</a>");
            }
            if (x.prevPage) {
                $("#pages").append("<a href='/gallery?page=" + (page-1) + "&reverse=" + reverse + "'>Previous</a>");
            }
            if (x.nextPage) {
                $("#pages").append("<a href='/gallery?page=" + (page+1) + "&reverse=" + reverse + "'>Next</a>");
            }
        }, null, false, { page: page, reverse: reverse });
}
//PICTURE
function bindTagSearch(apihost) {
    var tagThing = { length: -1, chars: "", data: [], isPerioded: false };
    const tagSearchForm = document.getElementById("tag-form");
    const tagSearchInput = document.getElementById("tags-to-add");
    const tagSuggestions = document.getElementById("tag-results");
    tagSearchInput.addEventListener("input", (e) => search(e, tagSuggestions, retrieve, tagThing, apihost));
    tagSearchForm.addEventListener("submit", (e) => e.target.value = "");
    tagSuggestions.addEventListener("click", (e) => submitEntry(e, tagSearchInput, tagThing));
}
function getMediaDetails(apihost, mediaID) {
    ajaxNonPost(apihost + "/api/media/details", "GET",
        a => {
            let tags = "";
            if (a.tags != null)
                a.tags.forEach(x => tags += x.name + " ");
            if (a.type.includes("image"))
                $("#base").append("<img src='" + apihost + "/" + a.path + "' alt='" + tags + "'>");
            if (a.type.includes("video"))
                $("#base").append("<video preload='metadata' loop controls autoplay muted src='" + apihost + "/" + a.path + "'>");
            makeList(a.tags);
            makeComments(apihost, a.comments);
        }, null, false, { id: mediaID });
}
function addComment(apihost, mediaID) {
    let commText = $("#new-comment").val();
    if (commText == null | commText === "") {
        $(".modal-body").html("comment cannot be empty");
        $("#modal").addClass("active");
        return;
    }
    var f = new FormData(); f.set("hash", mediaID); f.set("commentText", commText);
    ajaxPost(apihost + "/api/comment/post",
        a => {
            $("#new-comment").val("");
            ajaxNonPost(apihost + "/api/comment/byId", "GET", x => makeComment(apihost, x), null, false, { id: a });
        },
        y => checkAuth(y), true, f);
}
function addTag(apihost, mediaID) {
    var f = new FormData(); f.set("id", mediaID); f.set("tags", $("#tags-to-add").val());
    ajaxPost(apihost + "/api/media/addTags",
        x => {
            makeList(x.items);
            $("#tags-to-add").val("");
        },
        y => {
            checkAuth(y);
            alert("Can't add tag: " + jQuery.parseJSON(y.responseText).value.bad_tag + "\nTags should only contain letters and numbers\nAnd be 3 to 32 characters long");
        }, true, f);
}
function remove(apihost, mediaID) {
    ajaxNonPost(apihost + "/api/media/remove?id=" + mediaID, "DELETE", x => location.replace("gallery"), y => {
        checkAuth(y);
        alert(jQuery.parseJSON(y.responseText).result);
    }, true, null);
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
//PICTURE SEARCH
function populateSearch(apihost, tags, page, reverse) {
    ajaxNonPost(apihost + "/api/media/byTag", "GET",
        x => {
            if (x.items.length == 0) {
                $("#resultCount").text("0 results found")
                return;
            }
            $("#resultCount").text(x.count + " results found");
            for (var i = 0; i < x.items.length; i++) {
                $("#smth").append("<a href='/gallery/picture?id=" + x.items[i].hash + "'>" + "<img src='" + apihost + "/" + x.items[i].thumb + "'>" + "</a>");
            }
            if (x.prevPage) {
                $("#pages").append("<a href='/gallery/search?tags=" + tags + "&page=" + (page-1) + "&reverse=" + reverse + "'>Previous</a>")
            }
            if (x.nextPage) {
                $("#pages").append("<a href='/gallery/search?tags=" + tags + "&page=" + (page+1) + "&reverse=" + reverse + "'>Next</a>")
            }
        }, null, false, { tags: tags, page: page, reverse: reverse });
}
//PICTURE UPLOAD
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
    e.preventDefault();
    ajaxPost(apihost + "/api/media/upload",
        x => {
            e.target.reset();
            ajaxNonPost(apihost + "/api/media/details", "GET",
                y => {
                    $(".modal-body").html("Uploaded:<br> <a href='/gallery/picture?id=" + y.hash + "'>" + "<img src='" + apihost + "/" + y.thumb + "'>" + "</a>");
                    $("#modal").addClass("active");
                },
                null, false, { id: x.value.item });
        },
        y => {
            checkAuth(y);
            $(".modal-body").html(jQuery.parseJSON(y.responseText).value.item);
            $("#modal").addClass("active");
        }, true, new FormData(e.target));
}
function handleUploadFrom(e, apihost) {
    e.preventDefault();
    ajaxNonPost(
        apihost + "/api/media/uploadfrom", "GET",
        x => {
            e.target.reset();
            ajaxNonPost(apihost + "/api/media/details", "GET",
                y => {
                    $(".modal-body").html("Uploaded:<br> <a href='/gallery/picture?id=" + y.hash + "'>" + "<img src='" + apihost + "/" + y.thumb + "'>" + "</a>");
                    $("#modal").addClass("active");
                },
                null, false, { id: x.value.item });
        },
        y => {
            checkAuth(y);
            e.target.reset();
            $(".modal-body").html(jQuery.parseJSON(y.responseText).value.item);
            $("#modal").addClass("active");
        },
        true, { source: $("#link-input").val() });
}
//USER HOMEPAGE
function getLoggedUserDetails(apihost) {
    ajaxNonPost(apihost + "/api/user/getInfo", "GET",
        x => {
            $("#user-info").append("<p>" + x.username + "</p>");
            $("#user-info").append("<p>Registered: " + new Date(x.dateRegistered * 1000) + "</p>");
            $("#user-info").append("<p>User group: " + x.role + "</p>");
        },
        y => checkAuth(y), true);
    ajaxNonPost(apihost + "/api/user/getSessions", "GET",
        x => {
            x.allSessions.reverse();
            x.allSessions.forEach(function (item) {
                $("#base").append("<details id='" + item.id + "'><summary>Session " + item.id + "</summary><p>Last Active: " + new Date(item.lastActivity * 1000) + "Ip Address: " + item.ip + "UserAgent: " + item.userAgent + (item.isActiveSession ? "Active" : "<button onclick='closeSession(\"" + apihost + "\",\"" + item.id + "\");'>Close</button>") + "</p></details>");
            });
        },
        y => checkAuth(y), true);
    ajaxNonPost(apihost + "/api/comment/mine", "GET",
        x => {
            if (x.value.items != null)
                x.value.items.forEach(i => makeComment(apihost, i, true));
        },
        y => checkAuth(y), true);
}
function signOff(apihost) {
    ajaxNonPost(apihost + "/api/user/signoff", "GET", x => window.location.href = "/user/login", x => alert("Something went wrong, try again"), true, { fromAJAX: true });
}
function closeSession(apihost, session) {
    ajaxNonPost(apihost + "/api/user/closeSession", "GET", x => $("#" + session + "").remove(), x => alert("Something went wrong!"), true, { sessionId: session });
}
//USER LOGIN
function bindLogin(apihost) {
    document.getElementById("login-form").addEventListener("submit", (e) => processLogin(e, apihost));
}
function processLogin(e, apihost) {
    e.preventDefault();
    ajaxPost(apihost + "/api/user/signin", x => window.location = "/gallery",
        (a, b, c) => console.log(`$first: ${a.responseText} second: ${b} third: ${c}`),
        true, new FormData(e.target));
}
//USER REGISTER
function bindRegister(apihost) {
    document.getElementById("register-form").addEventListener("submit", (e) => processRegister(e, apihost));
}
function processRegister(e, apihost) {
    e.preventDefault();
    ajaxPost(apihost + "/api/user/signup", x => window.location = "/gallery",
        (a, b, c) => console.log(`$first: ${a.responseText} second: ${b} third: ${c}`),
        true, new FormData(e.target));
}