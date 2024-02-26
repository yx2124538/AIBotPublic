﻿using aibotPro.Dtos;
using aibotPro.Models;
using OpenAI.ObjectModels.RequestModels;

namespace aibotPro.Interface
{
    public interface IWorkShop
    {
        //安装插件
        bool InstallPlugin(string account, int pluginId, out string errormsg);

        //安装自己插件
        bool InstallMyPlugin(string account, int pluginId, out string errormsg);

        //卸载插件
        bool UninstallPlugin(string account, int pluginId, out string errormsg);

        //获取插件列表
        List<Plugin> GetPlugins(string account);

        //分页查询所有插件
        List<Plugin> GetWorkShopPlugins(int page, int pageSize, string name, out int total);

        //作者删除插件
        bool DeletePlugin(string account, int pluginId, out string errormsg);

        //获取插件信息
        WorkShopPlugin GetPlugin(int pluginId, string account, string pcode = "", string pfunctionName = "");

        //获取插件安装列表
        List<Plugin> GetPluginInstall(string account);

        //获取插件的参数列表
        List<PluginParamDto> GetPluginParams(int pluginId);
        //获取插件的headers列表
        List<PluginHeaderDto> GetPluginHeaders(int pluginId);
        //获取插件的cookies列表
        List<PluginCookieDto> GetPluginCookies(int pluginId);

        //运行插件
        Task<PluginResDto> RunPlugin(string account, FunctionCall fn);
    }
}
