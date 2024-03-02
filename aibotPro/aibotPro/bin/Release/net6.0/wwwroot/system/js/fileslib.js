﻿$(function () {
    $('.nav-sub-link').removeClass('active');
    $('.nav-link').removeClass('active');
    $("#files-main-menu").addClass('active');
    $("#files-main-menu").parent().toggleClass('show');
    $("#files-main-menu").parent().siblings().removeClass('show');
    $("#lib-files-nav").addClass('active');
    getFiles('init');
});
let page = 1;
let pageSize = 10;
let noMoreData = false;
async function uploadChunk(file, chunk, chunks, start, chunkSize) {
    var end = Math.min(start + chunkSize, file.size);
    var chunkBlob = file.slice(start, end);
    var formData = new FormData();
    formData.append('file', chunkBlob);
    formData.append('chunkNumber', ++chunk);
    formData.append('fileName', file.name);

    await $.ajax({
        url: '/FilesAI/Upload',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (data) {
            var progress = (chunk / chunks) * 100;
            $('#p1').css('width', progress + '%').attr('aria-valuenow', progress).text(Math.round(progress) + '%');
        }
    });

    if (chunk < chunks) {
        await uploadChunk(file, chunk, chunks, end, chunkSize);
    } else {
        // 在所有切片上传完成后触发
        $.ajax({
            url: '/FilesAI/MergeFiles',
            type: 'POST',
            data: JSON.stringify({ fileName: file.name, totalChunks: chunks }),
            contentType: 'application/json',
            success: function (response) {
                getFiles('init');
                $('#p1').text('上传完成');
                //移除文件框
                fileInput.remove();
                closeModal();
            }
        });
    }
}

async function uploadFiles() {
    var fileInput = document.createElement('input');
    fileInput.type = 'file';
    fileInput.id = 'fileInput';
    fileInput.style.display = 'none';
    document.body.appendChild(fileInput);

    // 添加事件监听器
    fileInput.addEventListener('change', function () {
        var file = fileInput.files[0];
        if (!file) {
            balert('请选择文件', 'danger', true, 2000, "center");
            return;
        }
        if (file.size > 30 * 1024 * 1024) {
            balert('文件大小不能超过30兆', 'danger', true, 2000, "center");
            return;
        }
        var allowedExtensions = ['txt', 'pdf', 'ppt', 'doc', 'docx', 'xls', 'xlsx'];
        var fileExtension = file.name.split('.').pop().toLowerCase();
        if (!allowedExtensions.includes(fileExtension)) {
            balert('只允许上传TXT, PDF, PPT, WORD, EXCEL文件', 'danger', true, 2000, "top");
            return;
        }
        var chunkSize = 100 * 1024; // 100KB
        var chunks = Math.ceil(file.size / chunkSize);
        var chunk = 0;
        openModal('上传文件中，请稍候...', `<div class="progress ht-20">
                        <div id="p1" class="progress-bar wd-25p" role="progressbar" style="width: 0%;" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100">0%</div>
                    </div>`);

        uploadChunk(file, chunk, chunks, 0, chunkSize);
    });

    // 触发 file input 元素点击事件
    fileInput.click();
}
function getFiles(type) {
    var name = $('#searchKey').val();
    if (type == 'init') {
        page = 1;
        pageSize = 10;
    }
    if (type == 'loadmore' && noMoreData) { // 加载更多但标志已表示没有更多数据
        balert('没有更多了', "info", false, 1500, "center");
        return; // 直接返回，不再进行请求
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
        url: '/FilesAI/GetFilesLibs',
        data: data,
        success: function (res) {
            if (res.success) {
                var html = '';
                for (var i = 0; i < res.data.length; i++) {
                    var item = res.data[i];
                    var fileType = item.fileType;
                    var avatarpath = '';
                    if (fileType == ".txt")
                        avatarpath = '/static/image/TXTimg.png';
                    else if (fileType == ".pdf")
                        avatarpath = '/static/image/PDF.png';
                    else if (fileType == ".pptx")
                        avatarpath = '/static/image/PPT.png';
                    else if (fileType == ".doc" || fileType == ".docx")
                        avatarpath = '/static/image/DOC.png';
                    else if (fileType == ".xls" || fileType == ".xlsx")
                        avatarpath = '/static/image/XLS.png';
                    else
                        avatarpath = '';
                    html += '<div class="col-lg-3 col-md-6 col-sm-12 mb-4 grid-item">';
                    html += '<div class="card h-100">';
                    html += '<img class="card-img-top" style="width: 50px;height: 50px;margin:10px auto;" src="' + avatarpath + '">';
                    html += '<div class="card-body">';
                    html += '<h5 class="card-title" style="max-height: 100px; overflow: auto;">' + item.fileName + '</h5>';
                    html += '<p class="card-text">' + item.createTime + '</p>';
                    html += '<div class="d-flex justify-content-center">';
                    html += `<a href="#" class="btn btn-danger" style="margin-right:10px;" onclick="deleteFiles('` + item.fileCode + `')">删除</a>`;
                    html += '</div>';
                    html += '</div>';
                    html += '</div>';
                    html += '</div>';
                }
                if (type == 'loadmore') {
                    $('#masonry-layout').append(html);
                    if (res.data.length < pageSize) {
                        noMoreData = true;
                    }
                } else
                    $('#masonry-layout').html(html);
            }
        }
    });
}
function deleteFiles(fileCode) {
    showConfirmationModal('警告', '确认删除文件？', function () {
        //发送请求
        $.ajax({
            type: 'Post',
            url: '/FilesAI/DeleteFilesLibs',
            data: { fileCode: fileCode },
            success: function (res) {
                if (res.success) {
                    balert('删除成功', "success", false, 1500);
                    getFiles('init');
                }
            }
        });
    })
}

//function throttle(func, limit) {
//    var lastFunc, lastRan;
//    return function () {
//        var context = this, args = arguments;
//        if (!lastRan) {
//            func.apply(context, args);
//            lastRan = Date.now();
//        } else {
//            clearTimeout(lastFunc);
//            lastFunc = setTimeout(function () {
//                if ((Date.now() - lastRan) >= limit) {
//                    func.apply(context, args);
//                    lastRan = Date.now();
//                }
//            }, limit - (Date.now() - lastRan));
//        }
//    }
//}

//$(window).scroll(throttle(function () {
//    if ($(window).scrollTop() + $(window).height() >= $(document).height() && !isLoading) {
//        isLoading = true;
//        getFiles('loadmore');
//    }
//}, 500)); // limit to run every 250 milliseconds
$(document).keypress(function (e) {
    if ($("#searchKey").is(":focus")) {
        if (e.which == 13) {
            // 避免回车键换行
            e.preventDefault();
            getFiles('init');
        }
    }
});