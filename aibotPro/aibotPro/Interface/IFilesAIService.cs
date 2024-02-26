﻿using aibotPro.Models;

namespace aibotPro.Interface
{
    public interface IFilesAIService
    {
        bool SaveFilesLib(FilesLib filesLib);//保存文件库
        List<FilesLib> GetFilesLibs(int page, int pageSize, string name, out int total, string account = "");//分页获取文件库
        bool DeleteFilesLibs(string fileCode, string account);//删除文件库文件
        Task<string> PromptFromFiles(List<string> path, string account);//从文件库获取提示
    }
}
