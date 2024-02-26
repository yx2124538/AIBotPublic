﻿using aibotPro.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using aibotPro.Interface;
using Newtonsoft.Json;
using aibotPro.AppCode;
using aibotPro.Service;
using aibotPro.Dtos;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IRedisService _redis;
        private readonly AIBotProContext _context;
        private readonly ISystemService _systemService;
        private readonly JwtTokenManager _jwtTokenManager;
        private readonly IUsersService _usersService;
        private readonly IAiServer _ai;
        public HomeController(ILogger<HomeController> logger, IRedisService redisService, AIBotProContext context, ISystemService systemService, JwtTokenManager jwtTokenManager, IUsersService usersService, IAiServer ai)
        {
            _logger = logger;
            _redis = redisService;
            _context = context;
            _systemService = systemService;
            _jwtTokenManager = jwtTokenManager;
            _usersService = usersService;
            _ai = ai;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Midjourney()
        {
            return View();
        }
        public IActionResult DALL()
        {
            return View();
        }
        public IActionResult FromSton()
        {
            return View();
        }
        public IActionResult ChatSetting()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        //从Redis中获取AI模型信息
        //不允许匿名访问
        [Authorize]
        [HttpPost]
        public IActionResult GetAImodel()
        {
            List<AImodel> aiModel_lst = new List<AImodel>();
            //查询是否有对话设置
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            var chatSetting = _usersService.GetChatSetting(username);
            if (chatSetting != null && chatSetting.MyChatModel != null && chatSetting.MyChatModel.Count > 0)
            {
                foreach (var item in chatSetting.MyChatModel)
                {
                    AImodel aiModel = new AImodel();
                    aiModel.ModelNick = item.ChatNickName;
                    aiModel.ModelName = item.ChatModel;
                    aiModel.BaseUrl = item.ChatBaseURL;
                    aiModel.ApiKey = item.ChatApiKey;
                    aiModel_lst.Add(aiModel);
                }
            }
            else
                aiModel_lst = _systemService.GetAImodel();
            //移除BaseURL和ApiKey
            aiModel_lst.ForEach(x =>
            {
                x.BaseUrl = string.Empty;
                x.ApiKey = string.Empty;
            });
            return Json(new
            {
                success = true,
                data = aiModel_lst
            });

        }

        //获取IP地址及信息
        [HttpPost]
        public IActionResult GetIPInfo()
        {
            var ip = HttpContext.Items["IP"] as string;
            var address = HttpContext.Items["IPAddress"] as string;
            return Json(new
            {
                success = true,
                ip = ip,
                address = address
            });
        }
        //接收异常信息写入日志
        [Authorize]
        [HttpPost]
        public IActionResult WriteLog(string msg)
        {
            //从token中获取用户名
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            _systemService.WriteLog(msg, Dtos.LogLevel.Error, username);
            return Json(new
            {
                success = true,
                msg = "写入成功"
            });
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> GetChatHistoriesList(int pageIndex, int pageSize, string searchKey)
        {
            List<ChatHistory> chatHistories = new List<ChatHistory>();
            //从token中获取用户名
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            chatHistories = await _ai.GetChatHistoriesList(username, pageIndex, pageSize, searchKey);
            return Json(new
            {
                success = true,
                data = chatHistories
            });
        }
        [Authorize]
        [HttpPost]
        //删除聊天记录
        public IActionResult DelChatHistory(string chatId)
        {
            //从token中获取用户名
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            _ai.DelChatHistory(username, chatId);
            return Json(new
            {
                success = true,
                msg = "删除成功"
            });
        }
        [Authorize]
        [HttpPost]
        public IActionResult ShowHistoryDetail(string chatId)
        {
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            var chatHistories = _ai.ShowHistoryDetail(username, chatId);
            return Json(new
            {
                success = true,
                data = chatHistories
            });
        }
        [Authorize]
        [HttpPost]
        public IActionResult StopGenerate(string chatId)
        {
            //修改缓存中的状态
            string key = chatId + "_process";
            _redis.SetAsync(key, "false", TimeSpan.FromHours(1));
            return Json(new
            {
                success = true,
                msg = "停止成功"
            });
        }
        [Authorize]
        [HttpPost]
        public IActionResult SaveImg(IFormFile file, string thisAiModel)
        {
            //以年月日生成文件路径
            string path = Path.Combine("wwwroot", "files/uploadImg", DateTime.Now.ToString("yyyyMMdd"));
            string fileName = _systemService.SaveFiles(path, file);
            //返回文件名
            return Json(new
            {
                success = true,
                data = fileName
            });
        }
        [Authorize]
        [HttpPost]
        public IActionResult DelChatGroup(string groupId)
        {
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            _ai.DelChatGroup(username, groupId);
            return Json(new
            {
                success = true,
                msg = "删除成功"
            });
        }
        [Authorize]
        [HttpPost]
        public IActionResult SaveChatSetting(ChatSettingDto chatSettingDto)
        {
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            bool mychatmodel = true;
            bool mydall = true;
            bool mymidjourney = true;
            bool systemsetting = true;
            //判断是否为清空
            if (chatSettingDto.MyChatModel == null || chatSettingDto.MyChatModel.Count == 0)
            {
                chatSettingDto.MyChatModel = new List<MyChatModel>();
                mychatmodel = false;
            }
            if (chatSettingDto.MyDall.ApiKey == null || chatSettingDto.MyDall.ApiKey == string.Empty || chatSettingDto.MyDall.BaseURL == null || chatSettingDto.MyDall.BaseURL == string.Empty)
            {
                chatSettingDto.MyDall = new MyDall();
                mydall = false;
            }
            if (chatSettingDto.MyMidjourney.ApiKey == null || chatSettingDto.MyMidjourney.ApiKey == string.Empty || chatSettingDto.MyMidjourney.BaseURL == null || chatSettingDto.MyMidjourney.BaseURL == string.Empty)
            {
                chatSettingDto.MyMidjourney = new MyMidjourney();
                mymidjourney = false;
            }
            if (!mychatmodel && !mydall && !mymidjourney && !systemsetting)
            {
                //清空
                return Json(new
                {
                    success = _usersService.DeleteChatSetting(username, out string errormsg2),
                    msg = errormsg2
                });
            }
            //保存
            return Json(new
            {
                success = _usersService.SaveChatSetting(username, JsonConvert.SerializeObject(chatSettingDto), out string errormsg),
                msg = errormsg
            });
        }
        [Authorize]
        [HttpPost]
        //获取对话设置
        public IActionResult GetChatSetting()
        {
            var username = _jwtTokenManager.ValidateToken(Request.Headers["Authorization"].ToString().Replace("Bearer ", "")).Identity?.Name;
            var chatSetting = _usersService.GetChatSetting(username);
            return Json(new
            {
                success = true,
                data = chatSetting
            });
        }
        [Authorize]
        [HttpPost]
        public IActionResult GetNotice()
        {
            var notice = _context.Notices.FirstOrDefault();
            return Json(new
            {
                success = true,
                data = notice.NoticeContent
            });
        }
        public IActionResult GetModelPrice()
        {
            var modelPrice = _context.ModelPrices.AsNoTracking().ToList();
            return Json(new
            {
                success = true,
                msg = "获取成功",
                data = modelPrice
            });
        }
    }
}
