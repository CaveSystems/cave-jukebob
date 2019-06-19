/*
    webTest.js - a class like  function contruct
*/
/*global window */
/*global $ */
/*global Handlebars */

var L = function(data) {
    window.console.log(data);
};


var webTest = (function() {
// private part
    
//    var urlParams = new window.URLSearchParams(window.location.search);
    var templates = {};

    var compileTemplates = function() {
        $('script[data-cave-template]').each(function() {
            templates[$(this).attr('data-cave-template')] = Handlebars.compile($(this).text());
        });
    };

     var ajaxRequest = function (url, delegate, mydata) {
            var done = function (ajaxResult, textStatus, jqXHR) {
                    if (typeof ajaxResult === 'string') {
                        ajaxResult = JSON.parse(ajaxResult);
                    }

                    if (this.deleg) {
                        this.deleg(ajaxResult, this.mydata, jqXHR);
                    }
//                    if (!mdb.status.pollHash) mdb.pollServer();
                },

                fail = function (data, status, text) {
                    var t = 'Ajax error: ' + status + " / " + text;
                    L(t);
                    if (data.responseJSON !== undefined) {
                        t += ' - ' + mdb.parseError(data.responseJSON);
                        if (text === 'Unauthorized' ) {
                            mdb.showForbidden();
                        }
                    }
                    mdb.removeTimers();
                    toastr.error(t, 'MDB Ajax Request', {timeOut: 5000, positionClass: "toast-bottom-full-width"});
                },
                ajaxData = {
                    url: url,
                    timeout: 20000,
                    deleg: delegate,
                    mydata: mydata,
                    headers: {},
                    success: done,
                    error: fail
                };

            L('Ajax: [' + url + ']');

            if (mdb.status.session) {
                if (mdb.status.session.id) {
                    ajaxData.headers.Session = mdb.status.session.id;
                }
                if (mdb.status.session.auth) {
                    ajaxData.headers.Authorization = 'Basic ' + btoa(mdb.status.session.auth.email + ':' + mdb.status.session.auth.password);
                }
            }

            $.ajax(ajaxData);
    };
    
    var buildTooltipHtml = function(data) {
        var res = '<div style="text-align: left">';
        for (var k in data) {
            if (data.hasOwnProperty(k)) {
                res += k + ': ' + data[k] + '<br>';
            }
        }
        res += '</div>';
        return res;
    };

    var fillUserList = function (caveJsonData) {
        var i,r, options;
        if (caveJsonData && caveJsonData.Users && caveJsonData.Values) {
        // Build lookup-table
            var lookupLevel = {};
            var lookupEmail = {};
            for (i=0; i < caveJsonData.Values.RowCount; i++) {
                r = caveJsonData.Values.Rows[i];
                lookupLevel[r.Value] = r.Name;
            }
            for (i=0; i < caveJsonData.EmailAddresses.RowCount; i++) {
                r = caveJsonData.EmailAddresses.Rows[i];
                lookupEmail[r.UserID] = r.Address;
            }
            for (i=0; i < caveJsonData.Users.RowCount; i++) {
                r = caveJsonData.Users.Rows[i];
                r.Level = lookupLevel[r.AuthLevel];
                r.Email = lookupEmail[r.ID];
            }
            fillTemplate('userList', caveJsonData.Users);
            for (i=0; i < caveJsonData.Users.RowCount; i++) {
                r = caveJsonData.Users.Rows[i];
                options = {
                    title : buildTooltipHtml(r),
                    html : true
                };
                $("#user-row-" + r.ID.toString()).tooltip(options);
            }
        }
    };

    var fillTemplate = function (name, data) {
        $('script[data-cave-template="' + name + '"]').replaceWith(templates[name](data));
    };

    var doLogin = function () {
        var email = $('#loginEmail').val(),
            password = $('#loginPassword').val(),
            done = function (ajaxResult) {
            /*
                var session = ajaxResult.CaveJSON.Values.Rows[0].Value;
                document.cookie = 'Session =' + session + '; path=/';
                */
                L(ajaxResult);
                L(document.cookie);
                //window.location.href = '/admin/userlist';
            },
            fail = function () {
                L('login failed!');
            },
            ajaxdata = {
                url: '/auth/account/login.json',
          //      deleg: delegate,
          //      mydata: mydata,
                headers: {
                    Authorization : 'Basic ' + btoa(email + ':' + password)
                },
                success: done,
                error: fail
            };
        $.ajax(ajaxdata);
    };

    var doLogout = function () {
        document.cookie = "Session=;path=/";
        window.location.href = '/index';
    };
// init

    compileTemplates();

// set click handlers


// public Part

    var _interface = {};
    _interface.fillTemplate = fillTemplate;
    _interface.fillUserList = fillUserList;
    _interface.doLogin = doLogin;
    _interface.doLogout = doLogout;
    return _interface;
})();
