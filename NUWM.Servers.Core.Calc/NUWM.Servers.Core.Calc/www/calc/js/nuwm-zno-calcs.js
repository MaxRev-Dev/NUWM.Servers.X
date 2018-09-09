(function () {
    'use strict';
    var id = 0,
        htmls = "",
        css1 = false,
        css2 = false,
        inited = false,
        reqsent = false,
        cardsInAnim = 'fadeInRight',
        cardsOutAnim = 'fadeOutLeft',
        inAnim = 'fadeInDown',
        outAnim = 'bounceOut',
        base = "calc.nuwm.edu.ua",
        lk = {
            zc_css: "http://"+base+"/calc/css/nuwm-zno.calcs.min.css",
            hlp: "http://"+base+"/api/calc?hlp=",
            anim_css: "http://"+base+"/calc/css/animate.css",
            fa_css: "https://maxcdn.bootstrapcdn.com/font-awesome/4.7.0/css/font-awesome.min.css",
            bs_css: "https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-beta.2/css/bootstrap.min.css",
            bs_js: "https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-beta.2/js/bootstrap.min.js",
            pop_js: "https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.12.3/umd/popper.min.js",
            jq_js: "https://ajax.googleapis.com/ajax/libs/jquery/3.2.1/jquery.min.js"
        },
        loc = {
            ua: {
                lthis: 'ua',
                js_opencalc: 'Відкрити калькулятор абітурієнта',
                js_entrant_calc: 'Калькулятор абітурієнта',
                js_chose: 'Виберіть предмет',
                js_vill: 'Я з сільської місцевості',
                js_prior: 'Пріоритет',
                js_avermark: 'Середній бал атестату',
                js_calculate: 'Розрахувати',
                js_close: 'Закрити',
                js_loader: 'Завантаження',
                js_sr_title: 'Нажаль Ваші бали занизькі для вступу на наші спеціальності',
                js_sr_text: 'Спробуйте підготуватись на наступний рік. Ми чекаємо на Вас!',
                js_detail: 'Детальніше',
                js_open: 'Читати повністю',
                js_nodata: 'немає даних',
                js_tp_200: 'Бали ЗНО - від 100 до 200',
                js_prior_ph: 'Пріоритет заяви 1 - 7',
                js_tp_12: 'Найвищий бал - 12',
                js_not_full: 'Потрібно заповнити всі поля',
                js_last_mark: 'Прохідний бал на місця державного замовлення в 2017 році',
                js_your_mark: 'Ваш конкурсний бал',
                js_greeting_title: '',
                js_greeting_desc: ''
            },
            ru: {
                lthis: 'ru',
                js_opencalc: 'Открыть калькулятор абитуриента',
                js_entrant_calc: 'Калькулятор абитуриента',
                js_chose: 'Выберите предмет',
                js_vill: 'Я из сельськой месности',
                js_prior: 'Подготовительные курсы НУВХП',
                js_avermark: 'Средний бал атестата',
                js_calculate: 'Рассчитать',
                js_close: 'Закрыть',
                js_loader: 'Загрузка',
                js_greeting_title: '',
                js_greeting_desc: '',
                js_sr_title: '',
                js_sr_text: '',
                js_detail: 'О специальности',
                js_open: 'Читать полностью',
                js_nodata: 'нет данных',
                js_tp_200: 'Балы ВНО - от 100 до 200',
                js_prior_ph: 'Максимальная оценка - 200',
                js_tp_12: 'Максимальная оценка - 12',
                js_not_full: 'Нужно заполнить все данные',
                js_last_mark: '',
                js_your_mark: ''
            },
            en: {
                lthis: 'en',
                js_opencalc: 'Open calculator for entrant',
                js_entrant_calc: 'Calculator of entrant',
                js_chose: 'Chose subject',
                js_vill: 'I`m from village',
                js_prior: 'NUWM preparatory courses',
                js_avermark: 'Average score of the certificate',
                js_calculate: 'Calculate',
                js_close: 'Close',
                js_loader: 'Loading',
                js_greeting_title: '',
                js_greeting_desc: '',
                js_sr_title: '',
                js_sr_text: '',
                js_detail: 'See more',
                js_open: 'Read full',
                js_nodata: 'N/A',
                js_tp_200: 'ZNO score - from 100 to 200',
                js_prior_ph: 'Maximum is 200',
                js_tp_12: 'Maximum is 12',
                js_not_full: 'You must to fill all inputs',
                js_last_mark: '',
                js_your_mark: ''
            }
        };

    function getVault() {
        if (window.localStorage) {
            if (localStorage.getItem("localizedYeap")) {
                return localStorage.getItem("localizedYeap");
            }
            window.localStorage.setItem("localizedYeap", 'ua');
        }
        return 'ua';
    }

    function setVault(e) {
        if (window.localStorage) {
            localStorage.setItem("localizedYeap", e);
        }
    }

    function jsLang(e) {
        switch (e) {
        case 3:
            {
                setVault('en');
                localize(loc.en);
                break;
            }
        case 2:
            {
                setVault('ru');
                localize(loc.ru);
                break;
            }
        case 1:
            {
                setVault('ua');
                localize(loc.ua);
                break;
            }
        default:
            break;
    }
    }

    function popovers() {
        setTimeout(function () {
            var t = loc[getVault()];
            for (var i = 1; i <= 3; i++) {
                jQuery('input[id=zc-s' + i + '-val').attr('title', t.js_tp_200);
                jQuery('input[id=zc-s' + i + '-val').popover('dispose');
            }
            jQuery('#zc-prior').attr('title', t.js_prior_ph);
            jQuery('#zc-AverAt').attr('title', t.js_tp_12);
            jQuery('#zc-prior').popover('dispose');
            jQuery('#zc-AverAt').popover('dispose');
            jQuery(function () {
                jQuery('[data-toggle="tooltip"]').tooltip()
            })
        }, 0);
    }

    function localize(e) {
        jQuery("button[data-target='#zno-calcs_overlay").text(e.js_opencalc);
        for (var i = 1; i <= 3; i++) {
            var t = jQuery('#zc-s' + i);
            if (t.text() === loc.en.js_chose ||
                t.text() === loc.ru.js_chose ||
                t.text() === loc.ua.js_chose
            )
                t.text(e.js_chose);
        }
        jQuery('.loader-text').text(e.js_loader);
        jQuery('#zc-submit').text(e.js_calculate);
        jQuery('#zc-prior-text').text(e.js_prior);
        jQuery('#zc-averMark-text').text(e.js_avermark);
        jQuery('#zc-vill-text').text(e.js_vill);
        jQuery('#zc-close').text(e.js_close);
        jQuery('#zno-calcs .modal-title').text(e.js_entrant_calc);
        jQuery('#zno-calcs').removeClass('ua en ru');
        jQuery('#zno-calcs').addClass(e.lthis);
        jQuery("a[href^='#zc_data").each(function (btn) {
            jQuery(this).text(e.js_detail);
        });
        jQuery("a[data-nx='lock").each(function (btn) {
            jQuery(this).text(e.js_open);
        });
        jQuery('#zno-calcs .zc-gt_title').text(e.js_greeting_title);
        jQuery('#zno-calcs .zc-gt_text').text(e.js_greeting_desc);
        popovers();
    }

    function reveal() {
        setTimeout(function () {
            if (htmls === "") {
                setTimeout(function () {}, 1500);
            }
            jQuery("#zno-calcs").html(htmls);
            jQuery("#zno-calcs").removeClass("loader");
        }, 0);
    }

    function DelayedScripts() {
        if (typeof (jQuery.fn.popover) === 'undefined') {
            loadjscssfile(lk.bs_js, "js");
        }
    }

    function createVariant(i, data, cnt) {
        var el = document.createElement("a");
        el.setAttribute('class', 'dropdown-item');
        el.setAttribute('href', 'javascript:void(0);');
        el.setAttribute('data-val', 'zc-s-tmp' + cnt++);
        el.setAttribute('data-parentid', i);
        el.textContent = data;
        el.addEventListener('click', function (e) {
            reEval(e);
        });
        jQuery('#zc-s' + i + '-sel').append(el);
    }

    function reEval(e) {
        setTimeout(function () {
            var elem = "<div class='zc_tmp_loader_wrapper'><div class='zno-calcs_loader-wrapper '> <div class='zno-calcs_loader-inner'></div> <p class='loader-text'>" + loc[getVault()].js_loader + "</p></div></div>";

            for (var i = 1; i <= 3; i++) {
                var ep = document.createElement('div');
                ep.setAttribute('class', 'zc_tmp_loader animated fadeIn');
                ep.innerHTML = elem;

                jQuery('#zc-s' + i + '-sel').html("");
                jQuery('#zc-s' + i + '-sel').append(ep);
            }
        }, 0);

        var btn = e.target;
        jQuery('#zc-s' + btn.dataset.parentid).text(btn.textContent);
        jQuery('#zno-calcs a[data-val=' + btn.dataset.val +']').remove();
        var all = [],
            lg = loc[getVault()].js_chose;
        all.push(jQuery('#zc-s1').text() ===
            lg ? "" : jQuery('#zc-s1').text());
        all.push(jQuery('#zc-s2').text() ===
            lg ? "" : jQuery('#zc-s2').text());
        all.push(jQuery('#zc-s3').text() ===
            lg ? "" : jQuery('#zc-s3').text());
        all = jQuery.grep(all, Boolean).join(',');

        jQuery.ajax('../../api/calc?hlp=' + all).done(function (data) {
            splitForDrops(data)
        });
    }

    function splitForDrops(data) {
        data = data.response;
        for (var i = 1; i <= 3; i++) {
            var cnt = 0;
            jQuery('#zc-s' + i + '-sel').html('');
            data.split(',').forEach(function (data) {
                createVariant(i, data, cnt);
            })
        };
    }

    function InitDrops() {
        jQuery.ajax(lk.hlp).done(function (data) {
            splitForDrops(data)
        });
    }

    function timeforBS() {
        var timer = setInterval(function () {
            if (typeof (jQuery.fn.popover) !== 'undefined') {
                clearInterval(timer);
                InitHandlers();
            }
        }, 50);

    }

    function InitHandlers() {
        for (var i = 1; i <= 3; i++) {
            var t = jQuery('#zc-local-' + i);
            t.on('click', function (e) {
                var l = e.currentTarget.id;
                jsLang(parseInt(l[l.length - 1]))
            });
        }
        if (window.localStorage) {
            if (localStorage.getItem("localizedYeap")) {
                localize(loc[getVault()]);
                jQuery('#zno-calcs').addClass(loc[getVault()]);
            }
        }
        for (var i = 1; i <= 3; i++) {
            jQuery('input[id=zc-s' + i + '-val]').on('change', function () {
                var t = parseInt(jQuery(this).val());
                if (t !== '') {
                    if (t > 200) {
                        jQuery(this).val(200);
                    } else if (t < 100) {
                        jQuery(this).val(100);
                    }
                }
            });
        }

        jQuery('#zc-prior').on('change', function () {
            var t = parseInt(jQuery(this).val());
            if (t !== '') {
                if (t > 7) {
                    jQuery(this).val(7);
                } else if (t < 1) {
                    jQuery(this).val(1);
                }
            }
        });
        jQuery('#zc-AverAt').on('change', function () {
            var t = parseInt(jQuery(this).val());
            if (t !== '') {
                if (t > 12) {
                    jQuery(this).val(12);
                } else if (t < 1) {
                    jQuery(this).val(1);
                }
                //  jQuery(this).parents('div').addClass('warning');
            }
        });
    }

    function loadjscssfile(t, s) {
        var a;
        if ("js" === s) {
            a = document.createElement("script");
            a.setAttribute("type", "text/javascript");
            a.setAttribute("src", t);
        } else if ("css" === s) {
            a = document.createElement("link");
            a.setAttribute("rel", "stylesheet");
            a.setAttribute("type", "text/css");
            a.setAttribute("href", t);

        }
        "undefined" !== typeof a && document.getElementsByTagName("head")[0].appendChild(a);
        if (a.addEventListener) {
            a.addEventListener('load', function () {
                if (a.getAttribute("href") === lk.zc_css) {
                    css1 = true;
                }
            }, false);
            a.addEventListener('load', function () {
                if (a.getAttribute("href") === lk.bs_css) {
                    css2 = true;
                }
            }, false);
            a.addEventListener('load', function () {
                if (a.getAttribute("src") === lk.pop_js) {
                    DelayedScripts();
                }
            }, false);
            a.addEventListener('load', function () {
                if (a.getAttribute("src") === lk.bs_js) {
                    InitHandlers();
                }
            }, false);
        }

        var timerId = setInterval(function () {
            if (css1 && css2 && !reqsent) {
                reqsent = true;
                jQuery.ajax({
                    url: "http://"+base+"/calc/zno-calcs-misc.html",
                    context: document.body,
                    success: function (e) {
                        htmls = e;
                        InitDrops();
                        reveal();
                        clearInterval(timerId);
                        setTimeout(timeforBS, 0);
                    }
                });
            }
        }, 50);

    }

    function CreateElement(t) {
        id += 1;
        var c_lock = jQuery('#zno-calcs').attr('class');
        var s = t.title,
            a = t.subtitle,
            c = (null == t.url ? "javascript:void(0);" : t.url),
            l = t.page_content.content["Професійні профілі випускників"],
            n = (null == l ? "" : l.join('')),
            o = (null == t.branch_name ? "[немає даних]" : t.branch_name.content[0]),
            i = (null == t.branch_name ? "'#'" : t.branch_name.url),
            r = document.createElement("div");
        r.setAttribute("class", "card my-3"), r.innerHTML = "<div class='card-header p-0' role='tab' id='zc_data_heading" + id + "'> <div class='zno-calcs_sp-data my-2'>  <p class='zno-calcs_sp-data_title'> <strong>" + s + "</strong></p>" + (null == a ? "" : "  <p class='zno-calcs_sp-data_subtitle'>" + a + "</p>") +
            "<div class='zc_marks container-fluid'> <p class='zc_last_mark' data-toggle='tooltip' data-placement='left' title='' >" + (t.aver_mark !== '0' ? t.aver_mark : loc[getVault()].js_nodata) + "</p> <p class='zc_your_mark' data-toggle='tooltip' data-placement='left' title='' >" + t.aver_mark_calc + "</p></div>" +
            " <div class='container-fluid'>   <div class='clearfix'> <p class='zno-calcs_sp-data_code float-left'>" + t.code + "</p>    <a class='collapsed btn btn-primary btn-sm float-right' data-toggle='collapse' data-parent='#zno-calcs_data' href='#zc_data_collapse" + id + "' aria-expanded='true' aria-controls='zc_data_collapse" + id + "'>" + loc[c_lock].js_detail + "</a>   </div>  </div> </div></div><div id='zc_data_collapse" + id + "' class='collapse' role='tabpanel'>   <div class='card-block m-3 clearfix'>       <h4 class='card-title'>" + s + "</h4>       <h6 class='card-subtitle mb-2 text-muted'>  " + (null == t.branch_name ? "<p class='card-subtile mb-2 text-muted'>[" + loc[c_lock].js_nodata + "]</p>" : "<a href='" + i + "' target='_blank' class='card-link'>" + o + "</a>") + "</h6>       <p class='card-text'>" + n + "</p>       <a href='" + c + "' target='_blank' data-nx='lock' class='card-link btn btn-sm btn-primary float-right'>" + loc[c_lock].js_open + "</a>   </div></div>", jQuery("#zno-calcs_data").append(r)
    }

    function BindAnimation() {
        var jQueryanimation_elements = jQuery('#zno-calcs .card');
        var jQuerywindow = jQuery(window);

        function check_if_in_view() {
            var window_height = jQuerywindow.height();
            var window_top_position = jQuerywindow.scrollTop();
            var window_bottom_position = (window_top_position + window_height);

            jQuery.each(jQueryanimation_elements, function () {
                var jQueryelement = jQuery(this);
                var element_height = jQueryelement.outerHeight();
                var element_top_position = jQueryelement.offset().top;
                var element_bottom_position = (element_top_position + element_height);

                if ((element_bottom_position >= window_top_position) &&
                    (element_top_position <= window_bottom_position)) {
                    jQueryelement.addClass(cardsInAnim);
                } else {
                    jQueryelement.removeClass(cardsInAnim);
                }
            });
        }
        jQuery("#zno-calcs .modal").off('scroll resize', check_if_in_view);

        jQuery("#zno-calcs .modal").on('scroll resize', check_if_in_view);
        jQuery("#zno-calcs .modal").trigger('scroll');
    }

    function SetObjects(t) {
        id = 0;
        setTimeout(function () {
            jQuery("#zno-calcs_data").empty();
            var up = loc[getVault()];
            jQuery('#zno-calcs .zc-gt_title').text(up.js_greeting_title);
            jQuery('#zno-calcs .zc-gt_text').text(up.js_greeting_desc);
            if (t.code !== 33) {
                t.response[0].speciality.forEach(function (item) {
                    CreateElement(item)
                });
                var tr = jQuery('.zc_your_mark');
                jQuery('.zc_your_mark').attr('title', up.js_your_mark);
                jQuery('.zc_your_mark').tooltip();
                jQuery('.zc_last_mark').attr('title', up.js_last_mark);
                jQuery('.zc_last_mark').tooltip();
                jQuery('#zno-calcs .card').addClass('animated');
                BindAnimation();
            } else {
                jQuery('.zc-gt_title').text(up.js_sr_title);
                jQuery('.zc-gt_text').text(up.js_sr_text);
            }
            jQuery('#zno-calcs .zno-calcs_loader').removeClass(inAnim);
            jQuery('#zno-calcs .zno-calcs_loader').addClass(outAnim);
            setTimeout(function () {
                jQuery('#zno-calcs .zno-calcs_loader').hide();
            }, 1000);
            jQuery('.zc-gt').fadeIn();
        }, 900);
    }
    var isAlert = false;

    function LoadCards() {
        for (var i = 1; i <= 3; i++) {
            if (jQuery('#zc-s' + i + '-val').val() === "") {
                if (window.localStorage) {
                    if (localStorage.getItem("localizedYeap") && !isAlert) {
                        isAlert = true;
                        var y = loc[getVault()];
                        var g = document.createElement('div');
                        g.setAttribute('class', 'alert alert-danger show animated bounceIn');
                        g.setAttribute('role', 'alert');
                        jQuery('#zno-calcs #alerter').append(g);
                        jQuery('#zno-calcs .alert').text(y.js_not_full);
                        jQuery('#zno-calcs .alert').alert();
                        setTimeout(function () {
                            jQuery('#zno-calcs .alert').removeClass('bounceIn');
                            jQuery('#zno-calcs .alert').addClass('bounceOut');

                            setTimeout(function () {
                                jQuery('#zno-calcs .alert').alert('close');
                                isAlert = false;
                            }, 1000);
                        }, 2000);
                    }
                }
                return;
            }
        }
        var all_names = [],
            all_values = [],
            vill = jQuery('#zc-vill').prop('checked') === true ? true : false,
            pr = jQuery('#zc-prior').val(),
            avm = jQuery('#zc-AverAt').val(),
            budget = !(jQuery('#zc-contract').prop('checked') === true ? true : false);
        for (var i = 1; i <= 3; i++) {
            all_names.push(jQuery('#zc-s' + i).text());
            all_values.push(jQuery('#zc-s' + i + '-val').val());
        }
        jQuery('#zno-calcs .card').removeClass(cardsInAnim);
        jQuery('#zno-calcs .card').addClass(cardsOutAnim);

        jQuery('#zno-calcs .zno-calcs_loader').show();
        jQuery('#zno-calcs .zno-calcs_loader').removeClass(outAnim);
        jQuery('#zno-calcs .zno-calcs_loader').addClass(inAnim);
        setTimeout(function () {
            jQuery('.zc-gt').fadeOut('slow');
        }, 200);
        jQuery.ajax({
            type: "json",
            data: "",
            url: "http://"+base+"/api/calc?n=" + all_names + '&v=' + all_values + '&vl=' + vill + (!!pr ? '&pr=' + pr : '') + (!!avm ? '&avm=' + avm : '') + '&b=' + budget,
            success: function (t) {
                SetObjects(t);
                if (jQuery(".zno-calcs").hasClass("zno-calcs_sidebar-active"))
                jQuery(".zno-calcs").toggleClass("zno-calcs_sidebar-active");
            }
        })
    }


    jQuery(document).ready(function () {
        getVault();
        
            if (typeof (window.jQuery.popover) == undefined || window.jQuery.fn.jquery!=='3.2.1') {
                var script = document.createElement('script');
                script.type = "text/javascript";
                script.src = lk.jq_js;
                document.getElementsByTagName('head')[0].appendChild(script);
            }
            loadjscssfile(lk.bs_css, "css"),
                loadjscssfile(lk.zc_css, "css"),
                loadjscssfile(lk.fa_css, "css"),
                loadjscssfile(lk.anim_css, "css");
            if (typeof (jQuery.fn.popover) == 'undefined') {
                loadjscssfile(lk.pop_js, "js")
            }

        }),
    jQuery(document).on("mouseenter", "*", function () {
        this.addEventListener("scroll", function () {
            var t = jQuery("#zno-calcs_toggler");
            jQuery(".modal").scroll(function () {
                jQuery(this).scrollTop() > 100 ? t.addClass("show") : t.removeClass("show")
            })
             jQuery(".zno-calcs").scroll(function () {
                jQuery(this).scrollTop() > 100 ? t.addClass("show") : t.removeClass("show")
            })
        })
        
    }),
    jQuery(document).on("click", "#zno-calcs_toggler", function () {
        jQuery(".zno-calcs").toggleClass("zno-calcs_sidebar-active");
    }),
    jQuery(document).on("click", "#zc-submit", function () {
        LoadCards();
    })
})();
