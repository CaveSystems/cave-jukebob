/*
    jukebob.js
*/
/*jshint esversion: 6 */
/*global window */
/*global $ */
/*global toastr */
/*global Handlebars */
/*global InPlaceEdit */
/*global CaveJSON */

toastr.options={
    timeOut: 4000,
    positionClass: "toast-top-center",
    preventDuplicates: false,
    showMethod: "slideDown",
    hideMethod: "slideUp"
};

var L = function(data) {
    window.console.log(data);
};

var getDocumentHeight = function() {
    let body = window.document.body;
    let html = window.document.documentElement;

    return Math.max(
        body.scrollHeight, body.offsetHeight,
        html.clientHeight, html.scrollHeight, html.offsetHeight
    );
};

var getScrollTop = function() {
    return (window !== undefined) ? window.pageYOffset :
        (window.document.documentElement || window.document.body.parentNode || window.document.body).scrollTop;
};


var jukeBob = (function() {
// private part

    let templates = {},
        selectors = {},
        callClientHook = function () {},
        callClientScroll = function () {},
        nextPageParams = false,
        pageRendered = false,
        nextPageUrl = false,
        loadNextPageOnScroll = false;

    const showError = function (msg, title, options) {
        let tOptions = {
            timeOut: 0,
            extendedTimeOut: 0,
            hideDuration: 200,
            closeButton: true,
            positionClass: "toast-bottom-full-width toast-center",
        };

        if (options !== undefined) {
            Object.assign(tOptions, options);
        }

        toastr.error(msg, title, tOptions);
    };


    const compileTemplates = function() {
        $('script[data-cave-template]').each(function() {
            let name = $(this).attr('data-cave-template');
            templates[name] = Handlebars.compile($(this).text().replace('>', ' data-cave-template="' + name +'">'));
        });
    };

    const registerHandlebarHelpers = function () {
        Handlebars.registerHelper('ifCond', function (v1, operator, v2, options) {
            switch (operator) {
                case '==':
                    return (v1 == v2) ? options.fn(this) : options.inverse(this);
                case '===':
                    return (v1 === v2) ? options.fn(this) : options.inverse(this);
                case '!=':
                    return (v1 != v2) ? options.fn(this) : options.inverse(this);
                case '!==':
                    return (v1 !== v2) ? options.fn(this) : options.inverse(this);
                case '<':
                    return (v1 < v2) ? options.fn(this) : options.inverse(this);
                case '<=':
                    return (v1 <= v2) ? options.fn(this) : options.inverse(this);
                case '>':
                    return (v1 > v2) ? options.fn(this) : options.inverse(this);
                case '>=':
                    return (v1 >= v2) ? options.fn(this) : options.inverse(this);
                case '&&':
                    return (v1 && v2) ? options.fn(this) : options.inverse(this);
                case '||':
                    return (v1 || v2) ? options.fn(this) : options.inverse(this);
                default:
                    return options.inverse(this);
            }
        });

        Handlebars.registerHelper('eachX', function(nr, mod, rem, options) {
            if (nr % mod === rem) {
                return options.fn(this);
            }
            return options.inverse(this);
        });

    };

    const showMessageBox = function (data) {
        if (data) {
            if (data.params) {
                CaveJSON.MessageBox = data.params;
            } else {
                CaveJSON.MessageBox = data;
            }
        }
        if (CaveJSON.MessageBox) {
            $('[data-cave-template="caveMessageBox"]').replaceWith(templates.caveMessageBox(CaveJSON));
            $('#caveMessageBox').modal({backdrop: 'static'});
        }
    };

    const addSearchParamAndReload = function (queryString) {
        CaveJSON.Params.f = queryString;
        window.location.href = window.location.pathname + '?' + $.param(CaveJSON.Params);
    };

    const addParamsAndReload = function (data) {
        Object.assign(CaveJSON.Params, data.params);
        window.location.href = window.location.pathname + '?' + $.param(CaveJSON.Params);
    };

    const removeParamsAndReload = function (data) {
        for (let name in data.params) {
            if (data.params.hasOwnProperty(name)) {
                delete CaveJSON.Params[name];
            }
        }
        window.location.href = window.location.pathname + '?' + $.param(CaveJSON.Params);
    };

    const callTemplate = function (name, data) {
         if (templates[name] !== undefined) {
            return templates[name](data);
        }
    };

    const fillTemplate = function (name) {
        if (templates[name] !== undefined) {
            $('[data-cave-template="' + name + '"]').replaceWith(templates[name](CaveJSON));
        }
        if (pageRendered) {
            setCaveHooks();
        }
    };

    const fillNewPage = function(ajaxData) {
        CaveJSON.parseNewData(ajaxData.CaveJSON);
        let pageData = callTemplate('page', CaveJSON);
        $('#page-' + CaveJSON.Pagination.Page).replaceWith(pageData);
        setCaveHooks();
        if (nextPageUrl) window.onscroll = onScroll;
    };

    // TODO remove design elemets from java script
    const setBGNowPlaying = function () {
        if (CaveJSON.NowPlaying && CaveJSON.NowPlaying.AudioFileID) {
            $('body').addClass('parallax-bg').css('background-image', 'url(/mdb/image/artist/get?background=true&audioFileID=' + CaveJSON.NowPlaying.AudioFileID + ')');
            $('#pageContainer').css('background-color', 'rgba(0,0,0,0.5)');
        }
    };

    const renderPage = function (delegate) {
//        L('render Page');
        fillTemplate('navigation');
        // show Error template if webresult error
        if (!CaveJSON.resultOK) {
            $('[data-cave-template="main"]').replaceWith(templates.errorMsg(CaveJSON));
            return;
        }
        fillTemplate('main');
        if (CaveJSON.Params.debug)
            fillTemplate('cave-debug');
        fillTemplate('footer');
        setCaveHooks();
        setBGNowPlaying();
        if (typeof delegate === 'function') {
            callClientHook = delegate;
            delegate();
        }
        pageRendered = true;
        if (CaveJSON.Params.debug === undefined) pollServer();
    };

    const loadNextPage = function () {
        if (nextPageUrl && CaveJSON.Pagination) {
            let p = (CaveJSON.Pagination.Page + 1);
            if (p < CaveJSON.Pagination.PageCount) {
                // L('load new Page: ' + p);
                jukeBob.setScroll();
                let params = CaveJSON.mapTemplateParameters(nextPageUrl, CaveJSON.Params);
                params.page = p;
                $('#pageContainer').append(callTemplate('pageFiller', {pageID: p.toString()}));
                doAjax({
                    url: nextPageUrl + '.json',
                    data: params,
                    success: fillNewPage
                });
            } else if ((p === CaveJSON.Pagination.PageCount) || (CaveJSON.Pagination.PageCount === 0)) {
                $('#pageContainer').css('margin-bottom', '100vh');
            }
        }
    };


    const setCaveHooks = function () {
        // set internal hooks
        $('[data-cave-click]').off('click').on('click', onClick);
        //$('[data-cave-keyUp]').off('keyup').on('keyup', jukeBob.onKeyUp);
        $('[data-cave-enterPressDoClick]').off('keypress').on('keypress', onEnterPressDoClick);
        $('[data-cave-keyEnter]').off('keypress').on('keypress', onKeyEnter);
        $('[data-InPlaceEdit]').each(function () {
            new InPlaceEdit($(this), jukeBob.templates.inPlaceEdit);
        });
        $('[data-cave-select2]').each(createSelection2);
    };

    const onEventDeleg = function (data) {
        data = data.split(',');
        let deleg = data.shift(),
            params = {},
            kv,key;
        if (typeof jukeBob[deleg] !== 'function') {
            L('[' + deleg + '] is not a function!');
            return;
        }
        for (let i = 0; i < data.length; i++) {
            if (data[i].includes('=')) {
                kv = data[i].split('=');
                key = kv.shift();
                // if value contains more '=' join the rest
                params[key] = kv.join('=');
            }
            else if (data[i].includes('<')) {
                kv = data[i].split('<');
                key = kv.shift();
                // if value contains more '>' join the rest
                let lookup = kv.join('<');
                // test for radio buttons
                let rb = $('input[name=' + lookup + ']:checked');
                let sel = $('select[id=' + lookup + ']');
                if (rb && rb.length === 1) {
                    params[key] = rb.val();
                } else if (sel && sel.length === 1 && sel.hasClass('select2-hidden-accessible')) {
                    params[key] = sel.find(':selected').text();
                }
                else {
                    params[key] = $('#' + lookup).val();
                }
            }
            else if (data[i].includes('>')) {
                throw '">" is not implemented in onClick() in JukeBob';
            } else {
                params[data[i]] = $('#' + data[i]).val();
            }
        }
        let result = {};
        result.params = params;
        result.source = $(this);
        jukeBob[deleg](result);
    };

    const onClick = function (e) {
        L('onClick');
        let data = $(this).attr('data-cave-click');
        if (data !== undefined) onEventDeleg(data);
    };

    const onKeyEnter = function (e) {
        if (e.which === 13) {
            L('onKeyEnter');
            let data = $(this).attr('data-cave-keyEnter');
            if (data !== undefined) onEventDeleg(data);
        }
    };

    const onEnterPressDoClick = function (e) {
        if (e.which === 13) {
            L('onKeyEnterDoClick');
            let data = $(this).attr('data-cave-enterPressDoClick');
            L(data);
            if (data === undefined) return;
            data = data.split(',');
            let deleg = data.shift();
            if (deleg !== undefined && (deleg.toString().length > 0)) {
                $('#' + deleg).trigger('click');
            }
        }
    };

    const doAjax = function (ajaxData) {
        
  //      L(ajaxData);
        
        if (ajaxData && ajaxData.url) {
            if (ajaxData.headers === undefined) ajaxData.headers = {};
            ajaxData.headers.Session = CaveJSON.Session.ID;
        }
        
        if (ajaxData.error === undefined) {
            ajaxData.error = function (data, status, text) {
                let t = 'Ajax error: ' + status + " / " + text;
                let d = '';
                if (data.responseJSON !== undefined) {
                    d = data.responseJSON.CaveJSON.Messages.Rows[0].Content;
                    t += ' - ' + d;

                }
                L(t);
                showMessageBox({
                    Title: 'Error',
                    Text: d,
                    Details: status + " / " + text,
                    Danger: true
				});
            };
        }

        if (ajaxData.success === undefined) {
            ajaxData.success = function (data) {
                CaveJSON.parseNewData(data.CaveJSON);
                jukeBob.fillTemplate('main');
            };
        }

        $.ajax(ajaxData);
    };

    const doAjaxMsg = function (url, data, message, onError) {
        doAjax({
            url: url,
            data: data,
            success: message ?  function () {
                toastr.success(message);
            } : undefined,
            error: onError
        });
    };

    const pollServer = function (pHash) {
        if ((pHash === undefined) && (CaveJSON) && (CaveJSON.pollHash !== undefined)) pHash = CaveJSON.pollHash;
        let ajaxData = {
            url : '/mdb/player/state.json',
            timeout: 60000,
            data : (pHash !== undefined) ? {hash : pHash} :  undefined,
            oldHash : pHash,
            success : function (data) {
                let newHash = data.CaveJSON.PlayerStates.Rows[0].Hash;
                if (this.oldHash !== newHash) {
                    CaveJSON.parseNewData(data.CaveJSON);
                    jukeBob.setBGNowPlaying();
                    fillTemplate('footer');
                }
                jukeBob.pollServer(newHash);
            },
            error : function (data, status, text) {
                let t = 'Ajax Poll error: ' + status + " / " + text;
                if (data.responseJSON !== undefined) {
                    t += ' - ' + data.responseJSON.CaveJSON.Messages.Rows[0].Content;
                } else {
                    t += ' No data';
                }

                L(t);
                // try again in 10 sec
                window.setTimeout(function () {
                    jukeBob.pollServer();
                }, 10000);
            }
        };

        doAjax(ajaxData);
    };

    const formDataToObject = function (formData) {
        let result = {};
        $(formData).each(function(index, obj){
            result[obj.name] = obj.value;
        });
        return result;
    };

    const createSelection2 = function () {
        let sName = $(this).attr('data-cave-select2');
        if ((selectors[sName] === undefined) && (CaveJSON.SelectorData[sName] !== undefined)) {
            selectors[sName] = $(this).select2(CaveJSON.SelectorData[sName]);
            selectors[sName].on('select2:select', onSelection2Change);
            if (CaveJSON.Params[CaveJSON.SelectorData[sName].pName]) {
                initSelection2(selectors[sName],CaveJSON.Params[CaveJSON.SelectorData[sName].pName]);
            }
        }
    };


    // TODO remove design from javascript
    const initSelection2 = function (sel2, data) {
        if (sel2.find("option[value='" + data + "']").length) {
            sel2.val(data).trigger('change');
        } else {
            const newOption = new Option(data, data, true, true);
            sel2.append(newOption).trigger('change');
        }
		sel2.parent().addClass('jb-bg-danger-3');
        sel2.next().find('b').css('border-top-color', '#fff');
    };

    const onSelection2Change = function (data) {
        let params = {};
        params[this.name] = data.params.data.id;
        addParamsAndReload( { params :  params });
    };

    const onScroll = function () {

        if (jukeBob.goToTopDiv) {
            jukeBob.goToTopDiv.toggle(window.scrollY > 150);
        } else {
            jukeBob.goToTopDiv = $('#jb-goToTopDiv');
            jukeBob.goToTopDiv.off('click').on('click', function () {
                window.scroll(0,0);
                jukeBob.goToTopDiv.toggle(false);
            });
        }

        if (getScrollTop() < getDocumentHeight() - (window.innerHeight)) return;
        loadNextPage();
    };


// init

    compileTemplates();
    registerHandlebarHelpers();

// public Part

    let _interface = {};
    _interface.templates = templates;
    _interface.showMessageBox = showMessageBox;
    _interface.showError = showError;
    _interface.addParamsAndReload = addParamsAndReload;
    _interface.removeParamsAndReload = removeParamsAndReload;
    _interface.setBGNowPlaying = setBGNowPlaying;
    _interface.callTemplate = callTemplate;
    _interface.fillTemplate = fillTemplate;
    _interface.renderPage = renderPage;
    _interface.loadNextPage = loadNextPage;
    _interface.pollServer = pollServer;
    _interface.setCaveHooks = setCaveHooks;
    _interface.doAjax = doAjax;
    _interface.doAjaxMsg = doAjaxMsg;
    _interface.createSelection2 = createSelection2;
    _interface.formDataToObject = formDataToObject;
    _interface.setClientHook = function (hook) {
        callClientHook = (typeof hook === 'function') ? hook : function () {};
    };
    _interface.setScroll = function (delegate) {
        if (typeof delegate === 'function') {
            callClientScroll = delegate;
            window.onscroll = onScroll;
        } else {
            callClientScroll = function () {};
            window.onscroll = null;
        }
    };
    _interface.setLoadNextPageOnScroll = function (url) {
        nextPageUrl = url;
        window.onscroll = onScroll;
        loadNextPage();
    };
    return _interface;
})();
