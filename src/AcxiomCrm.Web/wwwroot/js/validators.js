(function ($) {
    if (!$ || !$.validator) {
        return;
    }

    var phonePattern = /^[6-9][0-9]{9}$/;
    var localPartPattern = /^[A-Za-z0-9!#$%&'*+\-/=?^_`{|}~]+(\.[A-Za-z0-9!#$%&'*+\-/=?^_`{|}~]+)*$/;
    var domainLabelPattern = /^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?$/;
    var tldPattern = /^[A-Za-z]{2,}$/;

    function isValidPhone(value) {
        if (!value || typeof value !== 'string') {
            return false;
        }

        var cleaned = value.replace(/[\s\-\(\)]/g, '');
        if (cleaned.indexOf('+91') === 0) {
            cleaned = cleaned.substring(3);
        } else if (cleaned.indexOf('91') === 0 && cleaned.length === 12) {
            cleaned = cleaned.substring(2);
        } else if (cleaned.indexOf('0') === 0 && cleaned.length === 11) {
            cleaned = cleaned.substring(1);
        }

        return cleaned.length === 10 && phonePattern.test(cleaned);
    }

    function isValidEmail(value) {
        if (!value || typeof value !== 'string') {
            return false;
        }

        var trimmed = value.trim();
        if (trimmed.length > 254) {
            return false;
        }

        var atIndex = trimmed.indexOf('@');
        if (atIndex <= 0 || atIndex !== trimmed.lastIndexOf('@')) {
            return false;
        }

        var localPart = trimmed.substring(0, atIndex);
        var domainPart = trimmed.substring(atIndex + 1);

        if (localPart.length === 0 || localPart.length > 64 || !localPartPattern.test(localPart)) {
            return false;
        }

        if (domainPart.length === 0 || domainPart.length > 253) {
            return false;
        }

        var labels = domainPart.split('.');
        if (labels.length < 2) {
            return false;
        }

        for (var i = 0; i < labels.length - 1; i++) {
            var label = labels[i];
            if (label.length === 0 || label.length > 63 || !domainLabelPattern.test(label)) {
                return false;
            }
        }

        var tld = labels[labels.length - 1];
        if (!tldPattern.test(tld)) {
            return false;
        }

        return true;
    }

    $.validator.addMethod('axphone', function (value, element) {
        if (this.optional(element)) {
            return true;
        }
        return isValidPhone(value);
    }, 'Enter a valid phone number.');

    $.validator.addMethod('axemail', function (value, element) {
        if (this.optional(element)) {
            return true;
        }
        return isValidEmail(value);
    }, 'Enter a valid email address.');

    $.validator.addMethod('axfuturedate', function (value, element, params) {
        if (this.optional(element)) {
            return true;
        }
        if (!value) {
            return false;
        }
        var today = params;
        return value >= today;
    }, 'Date cannot be in the past.');

    if ($.validator.unobtrusive && $.validator.unobtrusive.adapters) {
        $.validator.unobtrusive.adapters.addBool('axphone');
        $.validator.unobtrusive.adapters.addBool('axemail');
        $.validator.unobtrusive.adapters.add('axfuturedate', ['today'], function (options) {
            options.rules['axfuturedate'] = options.params.today;
            options.messages['axfuturedate'] = options.message;
        });
    }
})(window.jQuery);
