/*
    InPlaceEdit.js
    switches an element with agiven tempplate form, and return a data to a delegate
*/
/*global window */
/*global $ */

var InPlaceEdit = (function () {
    'use strict';

    // constructor
    var InPlaceEdit = function (element, template, data, delegate) {
        this.id = "id-" + Math.random().toString(16).slice(2);
        this.element = element;
        if (typeof data === 'undefined') {
            data = this.getData(element);
        }
        this.data = data;
        this.data.id = this.id;
        this.template = template(data);
        if (typeof delegate === 'undefined') delegate = data.delegate;
        if (typeof delegate === 'function') {
            this.delegate = delegate;
        } else {
            this.delegate = function () {};
        }
        element.off('click').on('click', this, this.onEdit);
    };

    InPlaceEdit.prototype.getData = function (element) {
        var dataRef = element.attr('data-InPlaceEdit');
        if (dataRef !== undefined) {
            dataRef = dataRef.split('.');
            if (dataRef.length === 1 && (window[dataRef[0]] !== undefined)) {
                return window[dataRef[0]];
            } else if (dataRef.length === 2 && (window[dataRef[0]] !== undefined)) {
                var d = window[dataRef[0]][dataRef[1]];
                if (d.placeholder === undefined) {
                    d.placeholder = dataRef[1];
                }
                return d;
            }
        }
    };

    InPlaceEdit.prototype.onEdit = function (eventData) {
        var instance = eventData.data;
        if (instance && instance.element) {
            instance.element.hide();
            instance.element.after(instance.template);
            instance.submit = $('#submit-' + instance.id);
            instance.input = $('#input-' + instance.id);
            instance.cancel = $('#cancel-' + instance.id);
            instance.setHooks();
        }
    };

    InPlaceEdit.prototype.setHooks = function () {
        if (this.input) {
            this.input.focus();
            if (this.data.value) {
                this.input.val(this.data.value);
                this.input[0].select();
            }
            this.input.off('keypress').on('keypress', this, this.onKeyPress);
        }
        if (this.submit) {
            this.submit.off('click').on('click', this, this.onSubmit);
        }
        if (this.cancel) {
            this.cancel.off('click').on('click', this, this.onCancel);
        }
    };

    InPlaceEdit.prototype.onSubmit = function (eventData) {
        var instance = eventData.data;
        if (instance && instance.data) {
            if (!instance.data.params) {
                instance.data.params = {};
            }
            if (!instance.data.key) {
                instance.data.key = 'value';
            }
            if (instance.input && instance.delegate) {
                instance.data.params[instance.data.key] = instance.input.val();
                instance.delegate(instance.data);
                instance.doExit();
            }
        }

    };

    InPlaceEdit.prototype.onCancel = function (eventData) {
        var instance = eventData.data;
        if (instance && instance.data) {
            instance.doExit();
        }
    };

    InPlaceEdit.prototype.onKeyPress = function (eventData) {
        if (eventData.which === 13) {
            var instance = eventData.data;
            if (instance && instance.submit) {
                instance.submit.trigger('click');
            }
        }
    };

    InPlaceEdit.prototype.doExit = function () {
        this.element.next().remove();
        this.element.show();
    };

    return InPlaceEdit;
})();
