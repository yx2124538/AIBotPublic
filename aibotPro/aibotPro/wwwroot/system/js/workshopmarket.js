let page = 1;
let pageSize = 10;
$(function () {
    $('.nav-sub-link').removeClass('active');
    $('.nav-link').removeClass('active');
    $("#cygf-main-menu").addClass('active');
    $("#cygf-main-menu").parent().addClass('show');
    $("#cygf-main-menu").parent().siblings().removeClass('show');
    $("#workshopmarket-cygf-nav").addClass('active');
    getWorkShopPlugins('init');
});
window.onload = () => {
    var elem = document.querySelector('#masonry-layout');
    new Masonry(elem, {
        // 选项
        itemSelector: '.grid-item',
        columnWidth: '.grid-item',
        percentPosition: true
    });
};
$(window).scroll(function () {
    // 检查用户是否滚动到页面底部
    // window的滚动高度 + window的高度 >= 整个document的高度
    if ($(window).scrollTop() + $(window).height() >= $(document).height()) {
        getWorkShopPlugins('loadmore');
    }
});
$(document).keypress(function (e) {
    if ($("#searchKey").is(":focus")) {
        if (e.which == 13) {
            // 避免回车键换行
            e.preventDefault();
            getWorkShopPlugins('init');
        }
    }
});
function getWorkShopPlugins(type) {
    var name = $('#searchKey').val();
    if (type == 'init') {
        page = 1;
        pageSize = 10;
    }
    if (type == 'loadmore') {
        page++;
    }
    var data = {
        name: name,
        page: page,
        pageSize: pageSize
    };
    $.ajax({
        type: 'Post',
        url: '/WorkShop/GetWorkShopPlugins',
        data: data,
        success: function (res) {
            if (res.success) {
                var html = '';
                for (var i = 0; i < res.data.length; i++) {
                    var item = res.data[i];
                    html += '<div class="col-lg-3 col-md-6 col-sm-12 mb-4 grid-item">';
                    html += '<div class="card h-100">';
                    html += '<img class="card-img-top" style="width: 50px;height: 50px;margin:10px auto;" src="' + item.pavatar + '" alt="插件名称1">';
                    html += '<div class="card-body">';
                    html += '<h5 class="card-title">' + item.pnickname + '</h5>';
                    html += '<p class="card-text">' + item.pfunctioninfo + '</p>';
                    html += '<p class="card-text"><span>价格：</span><span style="color:#f2c044" class="plugin_price">' + item.pluginprice + '</span></p>';
                    html += '<div class="d-flex justify-content-center">';
                    html += `<a href="#" class="btn btn-primary" style="margin-right:10px;" onclick="insertPlugin(` + item.id + `,'` + item.pluginprice + `')">安装</a>`;
                    html += '<a href="#" class="btn btn-secondary" onclick="seePlugin(' + item.id + ')">查看</a>';
                    html += '</div>';
                    html += '</div>';
                    html += '</div>';
                    html += '</div>';
                }
                if (type == 'loadmore') {
                    $('#masonry-layout').append(html);
                    if (res.data.length < pageSize) {
                        balert('没有更多了', "info", false, 1500);
                        page--;
                    }
                } else
                    $('#masonry-layout').html(html);
                var elem = document.querySelector('#masonry-layout');
                new Masonry(elem, {
                    // 选项
                    itemSelector: '.grid-item',
                    columnWidth: '.grid-item',
                    percentPosition: true
                });
            }
        }
    });
}
function insertPlugin(id, price) {
    //二次确认
    showConfirmationModal('安装插件', '确认消耗<span style="color:#f2c044">【' + price + '】</span>安装此插件？（插件作者无需付费）', function () {
        //发起请求
        loadingOverlay.show();
        $.ajax({
            type: 'Post',
            url: '/WorkShop/InstallPlugin',
            data: { id: id },
            success: function (res) {
                loadingOverlay.hide();
                if (res.success) {
                    balert(res.msg, "success", false, 1500);
                } else {
                    balert(res.msg, "danger", false, 2000);
                }
            }
        });
    });
}

function seePlugin(id) {
    //发起请求
    $.ajax({
        type: 'Post',
        url: '/WorkShop/SeePlugin',
        data: { id: id },
        success: function (res) {
            if (res.success) {
                if (res.data == null) {
                    balert('无法查看该插件', "danger", false, 2000, "top");
                }
                else {
                    window.location.href = '/WorkShop/MyWork?plugincode=' + res.data.pcode + '&id=' + res.data.id + '&type=see';
                }
            } else {
                balert(res.msg, "danger", false, 2000);
            }
        }
    });
}
