﻿using System.Text.RegularExpressions;
using aibotPro.AppCode;
using aibotPro.Dtos;
using aibotPro.Interface;
using aibotPro.Models;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Builders;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;
using TiktokenSharp;
using static OpenAI.ObjectModels.StaticValues;
using LogLevel = aibotPro.Dtos.LogLevel;

namespace aibotPro.Service;

public class ChatHub : Hub
{
    private readonly IAiServer _aiServer;
    private readonly IAssistantService _assistantService;
    private readonly IBaiduService _baiduService;
    private readonly ChatCancellationManager _chatCancellationManager;
    private readonly AIBotProContext _context;
    private readonly IFilesAIService _filesAIService;
    private readonly IFinanceService _financeService;
    private readonly JwtTokenManager _jwtTokenManager;
    private readonly IRedisService _redis;
    private readonly ISystemService _systemService;
    private readonly IUsersService _usersService;
    private readonly IWorkShop _workShop;

    public ChatHub(JwtTokenManager jwtTokenManager, IUsersService usersService, ISystemService systemService,
        IRedisService redisService, IAiServer aiServer, IBaiduService baiduService, IWorkShop workShop,
        IFilesAIService filesAIService, AIBotProContext context, IFinanceService financeService,
        IAssistantService assistantService, ChatCancellationManager chatCancellationManager)
    {
        _jwtTokenManager = jwtTokenManager;
        _usersService = usersService;
        _systemService = systemService;
        _redis = redisService;
        _aiServer = aiServer;
        _baiduService = baiduService;
        _workShop = workShop;
        _filesAIService = filesAIService;
        _context = context;
        _financeService = financeService;
        _assistantService = assistantService;
        _chatCancellationManager = chatCancellationManager;
    }

    //基础对话模型交互
    public async Task SendMessage(ChatDto chatDto)
    {
        var startTime = DateTime.Now;
        var firstTime = "-1";
        var allTime = "-1";
        var isFirstResponse = true;
        var httpContext = Context.GetHttpContext();
        var token = string.Empty;
        token = httpContext?.Request.Query["access_token"];
        //缓存存入工作中状态
        var (semaphore, cancellationToken) = _chatCancellationManager.GetOrCreateToken(chatDto.chatgroupid);
        if (string.IsNullOrEmpty(token) || !_jwtTokenManager.isTokenValid(token) || string.IsNullOrEmpty(chatDto.ip))
        {
            // 如果没有令牌或者令牌无效或者ip为空，则断开连接
            Context.Abort();
            return;
        }

        //chatDto是否读取缓存
        if (!string.IsNullOrEmpty(chatDto.inputCacheKey))
        {
            var cache = await _redis.GetAsync(chatDto.inputCacheKey);
            await _redis.DeleteAsync(chatDto.inputCacheKey);
            if (!string.IsNullOrEmpty(cache))
            {
                chatDto = JsonConvert.DeserializeObject<ChatDto>(cache);
            }
        }

        //从token中获取账号信息
        var Account = string.Empty;
        if (!chatDto.isbot)
            Account = _jwtTokenManager.ValidateToken(token).Identity.Name;
        else
            Account = "robot_AIBOT";
        var chatId = string.Empty;
        var newChat = false;
        if (string.IsNullOrEmpty(chatDto.chatid))
        {
            chatId = Guid.NewGuid().ToString().Replace("-", ""); //创建chatid头部
            chatId = $"{chatId}U{Account}IP{chatDto.ip}";
            chatDto.chatid = chatId;
            newChat = true;
        }
        else
        {
            chatId = chatDto.chatid;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
        var chatRes = new ChatRes();
        chatRes.chatid = chatId;
        var senMethod = "ReceiveMessage";
        if (chatDto.isbot)
            senMethod = "ReceiveMessage_bot";
        //回应客户端就绪状态
        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
        var isVisionModel = false;
        var useMyKey = false;
        var imgTxt = string.Empty;
        var imgRes = string.Empty;
        var promptHeadle = chatDto.msg;
        //对话前的检查
        if (!await _usersService.ChatHubBeforeCheck(chatDto, Account, senMethod, chatId))
            return;
        try
        {
            var systemCfg = _systemService.GetSystemCfgs();
            //获取对话设置
            var chatSetting = _usersService.GetChatSetting(Account);
            //如果不使用历史记录
            if (chatSetting.SystemSetting.UseHistory == 0)
                newChat = true;
            //生成设置参数
            var apiSetting = new APISetting();
            var aImodels = new List<AImodel>();
            var delay = 0;
            if (chatSetting != null && chatSetting.MyChatModel != null && chatSetting.MyChatModel.Count > 0)
            {
                foreach (var item in chatSetting.MyChatModel)
                {
                    var aiModel = new AImodel();
                    aiModel.ModelNick = item.ChatNickName;
                    aiModel.ModelName = item.ChatModel;
                    aiModel.BaseUrl = item.ChatBaseURL;
                    aiModel.ApiKey = item.ChatApiKey;
                    aiModel.VisionModel = item.VisionModel;
                    if (aiModel.VisionModel.HasValue)
                        isVisionModel = aiModel.VisionModel.Value;
                    aImodels.Add(aiModel);
                }

                apiSetting.BaseUrl = aImodels.Where(x => x.ModelName == chatDto.aiModel).FirstOrDefault().BaseUrl;
                apiSetting.ApiKey = aImodels.Where(x => x.ModelName == chatDto.aiModel).FirstOrDefault().ApiKey;
                useMyKey = true;
            }
            else
            {
                //获取模型设置
                aImodels = _systemService.GetAImodel();
                if (aImodels != null)
                {
                    var useModel = aImodels.Where(x => x.ModelName == chatDto.aiModel).FirstOrDefault();
                    if (useModel != null)
                    {
                        apiSetting.BaseUrl = useModel.BaseUrl;
                        apiSetting.ApiKey = useModel.ApiKey;
                        if (useModel.VisionModel.HasValue)
                            isVisionModel = useModel.VisionModel.Value;
                        if (useModel.Delay.HasValue && useModel.Delay.Value >= 0)
                            delay = useModel.Delay.Value;
                    }
                    else
                    {
                        throw new Exception($"系统未配置{chatDto.aiModel}模型，请联系管理员");
                    }
                }
                else
                {
                    throw new Exception("系统未配置任何模型，请联系管理员");
                }
            }

            //生成AI请求参数
            var input = string.Empty;
            var output = string.Empty;
            var aiChat = new AiChat();
            var visionBody = new VisionBody();
            visionBody.stream = chatDto.stream;
            if (chatDto.seniorSetting)
            {
                SetUpParams(aiChat, chatDto);
                SetUpParams(visionBody, chatDto);
            }

            aiChat.Stream = chatDto.stream;
            var visionImg = new List<VisionImg>();
            if (chatDto.useMemory)
            {
                var embeddingModel = systemCfg.FirstOrDefault(x => x.CfgKey == "EmbeddingsModel");
                var memory = await _aiServer.GetMemory(embeddingModel.CfgValue, Account, promptHeadle);
                if (memory != null && memory.Data != null)
                {
                    chatDto.system_prompt = "我会使用数据库存储用户需要保留的历史记忆，以下是系统历史记忆,如果用户有需要可以取用：\n";
                    foreach (var item in memory.Data) chatDto.system_prompt += $"{item.VectorContent} \n";
                }
            }

            //如果有图片
            if (chatDto.image_path != null && chatDto.image_path.Count > 0)
            {
                var urlPattern = @"^(http|https)://";
                var imageAnalysisResults = new List<string>();
                var imageUrls = new List<string>();

                foreach (var imagePath in chatDto.image_path)
                {
                    var isUrl = Regex.IsMatch(imagePath, urlPattern, RegexOptions.IgnoreCase);
                    if (!isVisionModel)
                    {
                        var imageFullPath = isUrl ? imagePath : "wwwroot" + imagePath;
                        var imageData = await _systemService.ImgConvertToBase64(imageFullPath);
                        imgTxt = _baiduService.GetText(imageData);
                        imgRes = _baiduService.GetRes(imageData);
                        imageAnalysisResults.Add($"图像中的文字识别结果为：{imgTxt}, 图像中物体和场景识别结果为：{imgRes}");
                    }

                    if (chatDto.aiModel.Contains("-all"))
                    {
                        var imageUrl = isUrl
                            ? imagePath
                            : $"{Context.GetHttpContext().Request.Scheme}://{systemCfg.FirstOrDefault(x => x.CfgCode == "Domain")?.CfgValue}{imagePath.Replace("wwwroot", "")}"
                                .Replace("\\", "/");
                        imageUrls.Add(imageUrl);
                    }
                    else
                    {
                        if (isUrl)
                        {
                            var visionImgitem = new VisionImg()
                            {
                                url = imagePath
                            };
                            visionImg.Add(visionImgitem);
                        }
                        else
                        {
                            var imgBase64 = await _systemService.ImgConvertToBase64("wwwroot" + imagePath);
                            var dataHeader = "data:image/jpeg;base64,";
                            var visionImgitem = new VisionImg()
                            {
                                url = dataHeader + imgBase64
                            };
                            visionImg.Add(visionImgitem);
                        }
                    }
                }

                if (!isVisionModel)
                {
                    var combinedAnalysis = string.Join("\n", imageAnalysisResults);
                    promptHeadle = $"# 请根据以下多张图片的识别结果进行专业的分析回答：{combinedAnalysis}\n\n{promptHeadle}";
                }

                if (chatDto.aiModel.Contains("-all"))
                {
                    var combinedUrls = string.Join("\n", imageUrls);
                    promptHeadle = $"{chatDto.msg}\n\n图片链接:{combinedUrls}";
                }
            }

            if (chatDto.file_list != null && chatDto.file_list.Count > 0)
            {
                var fileContent = await _filesAIService.PromptFromFiles(chatDto.file_list, Account);
                if (chatDto.aiModel.Contains("-all"))
                {
                    promptHeadle += $"\n # 要求：{promptHeadle} \n\n";
                    for (var i = 0; i < chatDto.file_list.Count; i++)
                        promptHeadle +=
                            $"# 文件地址{i + 1}：{Context.GetHttpContext().Request.Scheme}://{systemCfg.Where(x => x.CfgCode == "Domain").FirstOrDefault().CfgValue}{chatDto.file_list[i].Replace("wwwroot", "").Replace("\\", "/")} \n\n";
                    input += fileContent;
                }
                else
                {
                    if (chatDto.readingMode)
                    {
                        string fileStr = string.Empty;
                        try
                        {
                            var readingFiles =
                                await _filesAIService.ReadingFiles(fileContent, promptHeadle, chatDto.chatid, Account,
                                    senMethod, 2000, cancellationToken);
                            promptHeadle += $"\n # 你是一个文件阅读专家 \n" +
                                            $"# 注意事项：\n" +
                                            $"**当涉及到图片内容时,请你以纯文本markdown形式原样输出图片,不要包裹代码块,直接文本形式输出图片的markdown字符串,并且注意图片和文字合理使用换行符换行,每张图独占一行以增加可读性** \n" +
                                            $"* 需要回答的问题如下： \n" +
                                            $"* {promptHeadle} \n" +
                                            $"* 参考的文件内容如下：\n";

                            foreach (var redingStr in readingFiles)
                            {
                                fileStr += redingStr;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            //await _systemService.WriteLog("阅读取消", Dtos.LogLevel.Info, Account);
                        }

                        promptHeadle += fileStr;
                    }
                    else
                    {
                        promptHeadle += $"\n # 你是一个文件阅读专家 \n" +
                                        $"# 注意事项：\n" +
                                        $"**当涉及到图片内容时,请你以纯文本markdown形式原样输出图片,不要包裹代码块,直接文本形式输出图片的markdown字符串,并且注意图片和文字合理使用换行符换行,每张图独占一行以增加可读性** \n" +
                                        $"* 需要回答的问题如下： \n" +
                                        $"* {promptHeadle} \n" +
                                        $"* 参考的文件内容如下：\n" +
                                        $"{fileContent}";
                    }
                }
            }

            input += promptHeadle;
            visionBody.model = chatDto.aiModel;
            aiChat.Model = chatDto.aiModel;
            var tmpmsg_v = new List<VisionChatMesssage>();
            var messages = new List<Message>();
            if (chatDto.chatid.Contains("gridview"))
                newChat = true;
            if (newChat)
            {
                if (!isVisionModel)
                {
                    //如果是新对话直接填充用户输入
                    var message = new Message();
                    if (!string.IsNullOrEmpty(chatDto.system_prompt))
                    {
                        message.Role = "system";
                        message.Content = chatDto.system_prompt;
                        messages.Add(message);
                        input += chatDto.system_prompt;
                    }

                    message = new Message();
                    message.Role = "user";
                    message.Content = promptHeadle;
                    messages.Add(message);
                }
                else
                {
                    //Vision
                    var promptvisionChatMesssage = new VisionChatMesssage();
                    var promptcontent = new List<VisionContent>();
                    var promptvisionContent = new VisionContent();

                    // 系统提示部分
                    if (!string.IsNullOrEmpty(chatDto.system_prompt))
                    {
                        promptvisionChatMesssage.role = "system";
                        promptvisionContent.text = chatDto.system_prompt;
                        promptcontent.Add(promptvisionContent);
                        promptvisionChatMesssage.content = promptcontent;
                        tmpmsg_v.Add(promptvisionChatMesssage);
                        input += chatDto.system_prompt;

                        // 重置为用户消息
                        promptvisionChatMesssage = new VisionChatMesssage();
                        promptcontent = new List<VisionContent>();
                        promptvisionContent = new VisionContent();
                    }

                    // 用户消息部分
                    promptvisionChatMesssage.role = "user";
                    promptvisionContent.text = promptHeadle;
                    promptcontent.Add(promptvisionContent);

                    // 添加图片，如果存在
                    if (visionImg != null && visionImg.Count > 0)
                    {
                        foreach (var visionImgitem in visionImg)
                        {
                            promptvisionContent = new VisionContent();
                            promptvisionContent.type = "image_url";
                            promptvisionContent.image_url = visionImgitem;
                            promptcontent.Add(promptvisionContent);
                        }
                    }

                    promptvisionChatMesssage.content = promptcontent;
                    tmpmsg_v.Add(promptvisionChatMesssage);
                }
            }
            else
            {
                //否则查询历史记录
                var historyCount = 5; //默认5
                if (chatSetting.SystemSetting.HistoryCount != 5)
                    historyCount = chatSetting.SystemSetting.HistoryCount;
                var chatHistories = _aiServer.GetChatHistories(Account, chatId, historyCount);
                //开始压缩的对话条数
                var startCompress = systemCfg.Where(x => x.CfgCode == "History_Prompt_Start_Compress").FirstOrDefault();
                if (startCompress == null)
                    chatDto.createAiPrompt = false;
                else if (chatHistories.Count < int.Parse(startCompress.CfgValue) * 2) //历史记录长度小于2强制关闭优化
                    chatDto.createAiPrompt = false;
                //遍历填充历史记录
                var systemPromptAdded = false; // 添加一个标志来控制系统提示词是否已填充
                var tempInput = string.Empty;
                foreach (var item in chatHistories)
                {
                    tempInput += item.Chat;
                    if (!isVisionModel)
                    {
                        var message = new Message();
                        if (!systemPromptAdded && !string.IsNullOrEmpty(chatDto.system_prompt))
                        {
                            message.Role = "system";
                            message.Content = chatDto.system_prompt;
                            messages.Add(message);
                            input += chatDto.system_prompt;
                            systemPromptAdded = true; // 更新标志状态，表明系统提示词已经添加
                        }

                        message = new Message();
                        message.Role = item.Role;
                        message.Content = item.Chat;
                        messages.Add(message);
                    }
                    else
                    {
                        // Vision
                        var hisvisionChatMesssage = new VisionChatMesssage();
                        var hiscontent = new List<VisionContent>();
                        var hisvisionContent = new VisionContent();
                        if (!systemPromptAdded && !string.IsNullOrEmpty(chatDto.system_prompt))
                        {
                            hisvisionChatMesssage.role = "system";
                            hisvisionContent.text = chatDto.system_prompt;
                            hiscontent.Add(hisvisionContent);
                            hisvisionChatMesssage.content = hiscontent;
                            tmpmsg_v.Add(hisvisionChatMesssage);
                            input += chatDto.system_prompt;
                            systemPromptAdded = true; // 更新标志状态
                        }

                        hisvisionChatMesssage = new VisionChatMesssage();
                        if (item.Chat.Contains("aee887ee6d5a79fdcmay451ai8042botf1443c04"))
                        {
                            // 分割文本和图片
                            var parts = item.Chat.Split(new[] { "aee887ee6d5a79fdcmay451ai8042botf1443c04" },
                                StringSplitOptions.None);

                            // 提取并填充文本内容
                            if (parts.Length > 0)
                            {
                                var textContent = new VisionContent();
                                textContent.type = "text";
                                textContent.text = parts[0];
                                hiscontent.Add(textContent);
                            }

                            // 提取并填充图片内容
                            if (parts.Length > 1)
                            {
                                for (int i = 1; i < parts.Length; i++)
                                {
                                    const string pattern = @"<img.+?src=[""'](.*?)[""'].*?>";
                                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);

                                    var matches = regex.Matches(parts[i]);

                                    foreach (Match match in matches)
                                    {
                                        var imgContent = new VisionContent()
                                        {
                                            type = "image_url",
                                            image_url = new VisionImg { url = match.Groups[1].Value }
                                        };
                                        hiscontent.Add(imgContent);
                                    }
                                }
                            }
                        }
                        else
                        {
                            hisvisionContent = new VisionContent();
                            hisvisionContent.text = item.Chat;
                            hiscontent.Add(hisvisionContent);
                        }

                        hisvisionChatMesssage.role = item.Role;
                        hisvisionChatMesssage.content = hiscontent;
                        tmpmsg_v.Add(hisvisionChatMesssage);
                    }
                }

                if (!isVisionModel)
                {
                    if (chatDto.createAiPrompt)
                    {
                        var message0 = new Message();
                        message0.Role = "user";
                        chatRes.message = "正在压缩历史记录📦...\n";
                        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                        message0.Content = await _aiServer.CreateHistoryPrompt(messages);
                        chatRes.message = "压缩完成✅ \n等待AI回复⏳ \n\n\n";
                        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                        if (!string.IsNullOrEmpty(message0.Content))
                        {
                            messages = new List<Message> { message0 };
                            input += message0.Content;
                        }
                        else
                        {
                            input += tempInput;
                            chatRes.message = "压缩失败❌ \n重新启用历史记录,等待AI回复⏳ \n\n\n";
                            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                        }
                    }
                    else
                    {
                        input += tempInput;
                    }

                    //填充用户输入
                    var message1 = new Message();
                    message1.Role = "user";
                    message1.Content = promptHeadle;
                    messages.Add(message1);
                }
                else
                {
                    if (chatDto.createAiPrompt)
                    {
                        var promptvisionChatMesssage0 = new VisionChatMesssage();
                        var promptcontent0 = new List<VisionContent>();
                        var promptvisionContent0 = new VisionContent();
                        chatRes.message = "正在压缩历史记录📦... \n";
                        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                        promptvisionContent0.text = await _aiServer.CreateHistoryPrompt(null, tmpmsg_v);
                        promptcontent0.Add(promptvisionContent0);
                        if (visionImg != null && visionImg.Count > 0)
                        {
                            foreach (var visionImgitem in visionImg)
                            {
                                promptvisionContent0 = new VisionContent();
                                promptvisionContent0.type = "image_url";
                                promptvisionContent0.image_url = visionImgitem;
                                promptcontent0.Add(promptvisionContent0);
                            }
                        }

                        promptvisionChatMesssage0.role = "user";
                        promptvisionChatMesssage0.content = promptcontent0;
                        if (!string.IsNullOrEmpty(promptvisionContent0.text))
                        {
                            tmpmsg_v = new List<VisionChatMesssage> { promptvisionChatMesssage0 };
                            input += promptvisionContent0.text;
                            chatRes.message = "压缩完成✅ \n等待AI回复⏳ \n\n\n";
                            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                        }
                        else
                        {
                            input += tempInput;
                            chatRes.message = "压缩失败❌ \n重新启用历史记录,等待AI回复⏳ \n\n\n";
                            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                        }
                    }
                    else
                    {
                        input += tempInput;
                    }

                    //Vision
                    var promptvisionChatMesssage = new VisionChatMesssage();
                    var promptcontent = new List<VisionContent>();
                    var promptvisionContent = new VisionContent();
                    promptvisionContent.text = promptHeadle;
                    promptcontent.Add(promptvisionContent);
                    if (visionImg != null && visionImg.Count > 0)
                    {
                        foreach (var visionImgitem in visionImg)
                        {
                            promptvisionContent = new VisionContent();
                            promptvisionContent.type = "image_url";
                            promptvisionContent.image_url = visionImgitem;
                            promptcontent.Add(promptvisionContent);
                        }
                    }

                    promptvisionChatMesssage.role = "user";
                    promptvisionChatMesssage.content = promptcontent;
                    tmpmsg_v.Add(promptvisionChatMesssage);
                }
            }

            aiChat.Messages = messages;
            visionBody.messages = tmpmsg_v.ToArray();
            if (!isVisionModel)
                visionBody = null;
            var sysmsg = string.Empty;
            try
            {
                if (chatDto.stream)
                {
                    await foreach (var responseContent in _aiServer.CallingAI(aiChat, apiSetting, chatDto.chatgroupid,
                                       visionBody, cancellationToken))
                    {
                        if (isFirstResponse)
                        {
                            firstTime = _systemService.CalculateTimeDifference(startTime, DateTime.Now).ToString("F1");
                            isFirstResponse = false;
                        }

                        if (semaphore.CurrentCount == 0)
                            // 被取消
                            break;
                        sysmsg += responseContent.Choices[0].Delta.Content;
                        chatRes.message = responseContent.Choices[0].Delta.Content;
                        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                        Thread.Sleep(delay);
                    }
                }
                else
                {
                    sysmsg = await _aiServer.CallingAINotStream(aiChat, apiSetting, visionBody);
                    firstTime = _systemService.CalculateTimeDifference(startTime, DateTime.Now).ToString("F1");
                    chatRes.message = sysmsg;
                    Thread.Sleep(delay);
                    await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                }
            }
            catch (OperationCanceledException)
            {
                //await _systemService.WriteLog("输出取消", Dtos.LogLevel.Info, Account); //输出取消
            }
            finally
            {
                allTime = _systemService.CalculateTimeDifference(startTime, DateTime.Now).ToString("F1");
                _chatCancellationManager.RemoveToken(chatDto.chatgroupid);
                // 保存对话记录
                if (chatDto.image_path != null && chatDto.image_path.Count > 0)
                {
                    foreach (var imagePath in chatDto.image_path)
                    {
                        chatDto.msg +=
                            $@"aee887ee6d5a79fdcmay451ai8042botf1443c04<br /><img src=""{imagePath.Replace("wwwroot", "")}"" style=""max-width:50%;"" />";
                    }
                }

                output = sysmsg;
                var tikToken = TikToken.GetEncoding("cl100k_base");
                if (chatDto.chatid.Contains("gridview"))
                {
                    sysmsg = sysmsg.ToLower();
                    if (sysmsg.Contains("```json"))
                    {
                        sysmsg = sysmsg.Split("```json")[1];
                        if (sysmsg.Contains("```")) sysmsg = sysmsg.Split("```")[0];
                    }

                    if (sysmsg.Contains("$schema"))
                    {
                        chatRes.message = $@"var spec = {sysmsg};
                                            vegaEmbed('.vis', spec)
                                                .then(result => console.log(result))
                                                .catch(error => console.error(error))";
                        //await Clients.Group(chatId).SendAsync(senMethod, JsonConvert.SerializeObject(hubRes));
                        chatRes.jscode = chatRes.message; //newFileName;
                        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                    }
                }

                chatRes.message = "";
                chatRes.isfinish = true;
                await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                await _aiServer.SaveChatHistory(Account, chatId, chatDto.msg, chatDto.msgid_u, chatDto.chatgroupid,
                    "user", chatDto.aiModel, firstTime, allTime);
                await _aiServer.SaveChatHistory(Account, chatId, sysmsg, chatDto.msgid_g, chatDto.chatgroupid,
                    "assistant", chatDto.aiModel, firstTime, allTime);
                if (!string.IsNullOrEmpty(output) && !useMyKey)
                    await _financeService.CreateUseLogAndUpadteMoney(Account, chatDto.aiModel,
                        tikToken.Encode(input).Count, tikToken.Encode(output).Count);
            }
        }
        catch (Exception e)
        {
            await ExceptionHandling(chatId, senMethod, e.Message);
        }
    }

    //创意工坊交互
    public async Task SendWorkShopMessage(ChatDto chatDto, bool onknowledge, List<string> typeCode)
    {
        var startTime = DateTime.Now;
        var firstTime = "-1";
        var allTime = "-1";
        var isFirstResponse = true;
        var httpContext = Context.GetHttpContext();
        var token = string.Empty;
        token = httpContext?.Request.Query["access_token"];
        if (string.IsNullOrEmpty(token) || !_jwtTokenManager.isTokenValid(token) || string.IsNullOrEmpty(chatDto.ip))
        {
            // 如果没有令牌或者令牌无效或者ip为空，则断开连接
            Context.Abort();
            return;
        }

        //chatDto是否读取缓存
        if (!string.IsNullOrEmpty(chatDto.inputCacheKey))
        {
            var cache = await _redis.GetAsync(chatDto.inputCacheKey);
            await _redis.DeleteAsync(chatDto.inputCacheKey);
            if (!string.IsNullOrEmpty(cache))
            {
                chatDto = JsonConvert.DeserializeObject<ChatDto>(cache);
            }
        }

        //从token中获取账号信息
        var Account = string.Empty;
        if (!chatDto.isbot)
            Account = _jwtTokenManager.ValidateToken(token).Identity.Name;
        else
            Account = "robot_AIBOT";
        var user = _usersService.GetUserData(Account);
        var chatId = string.Empty;
        var newChat = false;
        if (string.IsNullOrEmpty(chatDto.chatid))
        {
            chatId = Guid.NewGuid().ToString().Replace("-", ""); //创建chatid头部
            chatId = $"{chatId}U{Account}IP{chatDto.ip}";
            chatDto.chatid = chatId;
            newChat = true;
        }
        else
        {
            chatId = chatDto.chatid;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
        var chatRes = new ChatRes();
        chatRes.chatid = chatId;
        var senMethod = "ReceiveWorkShopMessage";
        if (chatDto.isbot)
            senMethod = "ReceiveWorkShopMessage_bot";
        //回应客户端就绪状态
        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
        var imgTxt = string.Empty;
        var imgRes = string.Empty;
        var input = string.Empty;
        var output = string.Empty;
        var promptHeadle = chatDto.msg;
        bool? visionModel = false;
        bool useMyKey = false;
        //对话前的检查
        if (!await _usersService.ChatHubBeforeCheck(chatDto, Account, senMethod, chatId))
            return;
        try
        {
            //获取对话设置
            var chatSetting = _usersService.GetChatSetting(Account);
            //如果不使用历史记录
            if (chatSetting.SystemSetting.UseHistory == 0)
                newChat = true;
            //生成设置参数
            var apiSetting = new APISetting();
            var delay = 0;
            var aImodels = new List<WorkShopAIModel>();
            //获取模型设置
            if (chatSetting != null && chatSetting.MyChatModel != null && chatSetting.MyChatModel.Count > 0)
            {
                foreach (var item in chatSetting.MyChatModel)
                {
                    var aiModel = new WorkShopAIModel();
                    aiModel.ModelNick = item.ChatNickName;
                    aiModel.ModelName = item.ChatModel;
                    aiModel.BaseUrl = item.ChatBaseURL;
                    aiModel.ApiKey = item.ChatApiKey;
                    aiModel.VisionModel = item.VisionModel;
                    if (aiModel.VisionModel.HasValue)
                        visionModel = aiModel.VisionModel.Value;
                    aImodels.Add(aiModel);
                }

                useMyKey = true;
            }
            else
            {
                aImodels = _systemService.GetWorkShopAImodel();
            }
            var openAiOptions = new OpenAiOptions();
            var channel = "OpenAI";
            if (aImodels != null)
            {
                var useModel = aImodels.Where(x => x.ModelName == chatDto.aiModel).FirstOrDefault();
                if (useModel != null)
                {
                    openAiOptions.BaseDomain = useModel.BaseUrl;
                    openAiOptions.ApiKey = useModel.ApiKey;
                    if (useModel.VisionModel.HasValue)
                        visionModel = useModel.VisionModel;
                    if (useModel.Delay.HasValue && useModel.Delay.Value >= 0)
                        delay = useModel.Delay.Value;
                    if (!string.IsNullOrEmpty(useModel.Channel))
                        channel = useModel.Channel;
                }
                else
                {
                    throw new Exception($"系统未配置{chatDto.aiModel}模型，请联系管理员");
                }
            }
            else
            {
                throw new Exception("系统未配置任何模型，请联系管理员");
            }

            var openAiService = new OpenAIService(openAiOptions);
            var systemPluginsInstallList = await _workShop.GetSystemPluginsInstall(Account);
            var mytools = new List<ToolDefinition>();
            var myplugins = new List<PluginDto>();
            if (onknowledge) //知识库检索状态
            {
                if (systemPluginsInstallList.Where(p => p.PluginName == "search_knowledge_base").FirstOrDefault() ==
                    null)
                {
                    chatRes.message = "尚未安装知识库检索插件，<a href='/WorkShop/WorkShopMarket'>【点击前往安装】</a>";
                    await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                    chatRes.message = "";
                    chatRes.isfinish = true;
                    await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                    return;
                }

                mytools.Add(ToolDefinition.DefineFunction(SystemPlugins.SysKnowledgeSearch));
                //mytools.Add(ToolDefinition.DefineFunction(fnGoogleSearch));
                chatDto.system_prompt = "你是知识库阅览专家，任何问题你先查询知识库，如知识库中无记录再自行结合上下文回答，请记住，先查询知识库";
            }
            else
            {
                if (!chatDto.isbot)
                    if (systemPluginsInstallList.Where(p => p.PluginName == "use_dalle3_withpr").FirstOrDefault() !=
                        null)
                        mytools.Add(ToolDefinition.DefineFunction(SystemPlugins.FnDall));
                if (systemPluginsInstallList.Where(p => p.PluginName == "search_google_when_gpt_cannot_answer")
                        .FirstOrDefault() != null)
                    mytools.Add(ToolDefinition.DefineFunction(SystemPlugins.FnGoogleSearch));
                //获取用户插件列表
                if (string.IsNullOrEmpty(chatDto.chatfrom))
                    myplugins = _workShop.GetPluginInstall(Account);
                else
                    myplugins = _workShop.GetPluginByTest(chatDto.chatfrom);
                if (myplugins != null && myplugins.Count > 0)
                    foreach (var pluginitem in myplugins)
                    {
                        var functionDefinition = new FunctionDefinition();
                        var myfn = new FunctionDefinitionBuilder(pluginitem.Pfunctionname, pluginitem.Pfunctioninfo);
                        //如果是工作流
                        if (pluginitem.Pcodemodel == "plugin-workflow")
                        {
                            //获取工作流节点数据
                            var nodeData = _context.WorkFlows.Where(x => x.Pcode == pluginitem.Pcode)
                                .FirstOrDefault().FlowJson;
                            //nodeData为空则跳过
                            if (string.IsNullOrEmpty(nodeData))
                                continue;
                            var workFlowNodeData = JsonConvert.DeserializeObject<WorkFlowNodeData>(nodeData);
                            //找到start节点
                            var homeData = workFlowNodeData.Drawflow.Home.Data;
                            var startData = homeData.Values.FirstOrDefault(x => x.Name == "start");
                            //寻找参数
                            if (startData.Data is StartData startDataSpecific)
                            {
                                // 现在startDataSpecific.Output指向StartOutput对象
                                var startOutput = startDataSpecific.Output;

                                if (startOutput != null)
                                    // 遍历PrItems
                                    foreach (var prItem in startOutput.PrItems)
                                        switch (prItem.PrType)
                                        {
                                            case "String":
                                                myfn.AddParameter(prItem.PrName,
                                                    PropertyDefinition.DefineString(prItem.PrInfo));
                                                break;
                                            case "Integer":
                                                myfn.AddParameter(prItem.PrName,
                                                    PropertyDefinition.DefineInteger(prItem.PrInfo));
                                                break;
                                            case "Boolean":
                                                myfn.AddParameter(prItem.PrName,
                                                    PropertyDefinition.DefineBoolean(prItem.PrInfo));
                                                break;
                                            case "Number":
                                                myfn.AddParameter(prItem.PrName,
                                                    PropertyDefinition.DefineNumber(prItem.PrInfo));
                                                break;
                                            default:
                                                myfn.AddParameter(prItem.PrName,
                                                    PropertyDefinition.DefineString(prItem.PrInfo));
                                                break;
                                        }
                            }
                        }
                        else
                        {
                            var myparams = _workShop.GetPluginParams(pluginitem.Id);
                            if (myparams != null && myparams.Count > 0)
                                foreach (var paramitem in myparams)
                                    switch (paramitem.ParamType)
                                    {
                                        case "String":
                                            myfn.AddParameter(paramitem.ParamName,
                                                PropertyDefinition.DefineString(paramitem.ParamInfo));
                                            break;
                                        case "Integer":
                                            myfn.AddParameter(paramitem.ParamName,
                                                PropertyDefinition.DefineInteger(paramitem.ParamInfo));
                                            break;
                                        case "Boolean":
                                            myfn.AddParameter(paramitem.ParamName,
                                                PropertyDefinition.DefineBoolean(paramitem.ParamInfo));
                                            break;
                                        case "Number":
                                            myfn.AddParameter(paramitem.ParamName,
                                                PropertyDefinition.DefineNumber(paramitem.ParamInfo));
                                            break;
                                        default:
                                            myfn.AddParameter(paramitem.ParamName,
                                                PropertyDefinition.DefineString(paramitem.ParamInfo));
                                            break;
                                    }
                        }

                        functionDefinition = myfn.Validate().Build();
                        mytools.Add(ToolDefinition.DefineFunction(functionDefinition));
                    }

                if (!chatDto.isbot)
                    chatDto.system_prompt = "如果用户的请求不清晰，可以要求澄清，也询问用户以明确是否需要调用函数。";
            }

            var visionMessageContent = new List<MessageContent>();
            //如果有图片
            if (chatDto.image_path != null && chatDto.image_path.Any())
            {
                foreach (var imagePath in chatDto.image_path)
                {
                    var urlPattern = @"^(http|https)://";
                    // 检查输入字符串是否匹配正则表达式
                    var isUrl = Regex.IsMatch(imagePath, urlPattern, RegexOptions.IgnoreCase);
                    var imageData = string.Empty;

                    if (visionModel.HasValue && visionModel.Value)
                    {
                        imageData = isUrl
                            ? imagePath
                            : await _systemService.ImgConvertToBase64(imagePath);
                        if (!isUrl) imageData = "data:image/jpeg;base64," + imageData;

                        // 如果visionModel为true，只需创建一次visionMessageContent
                        var messageContents = MessageContent.ImageUrlContent(
                            imageData,
                            ImageStatics.ImageDetailTypes.High
                        );
                        visionMessageContent.Add(messageContents);
                    }
                    else
                    {
                        imageData = await _systemService.ImgConvertToBase64(imagePath);
                        imgTxt = _baiduService.GetText(imageData);
                        imgRes = _baiduService.GetRes(imageData);
                        promptHeadle += $@"\n \n
                        * 图像中的文字识别结果为：{imgTxt} \n
                        * 图像中物体和场景识别结果为：{imgRes} \n";
                    }
                }

                promptHeadle = @$"# 要求：请你充当图片内容分析师,回答:{promptHeadle}";
            }

            var textMessageContent = MessageContent.TextContent(promptHeadle);
            visionMessageContent.Add(textMessageContent);
            input += promptHeadle;
            var chatMessages = new List<ChatMessage>();
            chatMessages.Add(ChatMessage.FromSystem(chatDto.system_prompt));
            if (newChat)
            {
                if (chatDto.image_path.Count > 0)
                    chatMessages.Add(ChatMessage.FromUser(visionMessageContent));
                else
                    chatMessages.Add(ChatMessage.FromUser(promptHeadle));
            }
            else
            {
                //否则查询历史记录
                var historyCount = 5;
                var chatHistories = _aiServer.GetChatHistories(Account, chatId, historyCount);

                //遍历填充历史记录
                foreach (var item in chatHistories)
                {
                    if (item.Chat.Contains("aee887ee6d5a79fdcmay451ai8042botf1443c04") && visionModel.HasValue &&
                        visionModel.Value)
                    {
                        var parts = item.Chat.Split(new[] { "aee887ee6d5a79fdcmay451ai8042botf1443c04" },
                            StringSplitOptions.None);
                        // 提取并填充图片内容
                        if (parts.Length > 1)
                        {
                            const string pattern = @"<img.+?src=[""'](.*?)[""'].*?>";
                            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

                            var hisvisionMessageContent = new List<MessageContent>
                            {
                                MessageContent.TextContent(parts[0])
                            };

                            for (int i = 1; i < parts.Length; i++)
                            {
                                var matches = regex.Matches(parts[i]);

                                foreach (Match match in matches)
                                {
                                    var imageUrl = match.Groups[1].Value;
                                    var urlPattern = @"^(http|https)://";
                                    var isUrl = Regex.IsMatch(imageUrl, urlPattern, RegexOptions.IgnoreCase);
                                    var imageData =
                                        isUrl ? imageUrl : await _systemService.ImgConvertToBase64(imageUrl);
                                    if (!isUrl) imageData = "data:image/jpeg;base64," + imageData;

                                    // 添加图片内容到消息内容列表
                                    hisvisionMessageContent.Add(MessageContent.ImageUrlContent(
                                        imageData,
                                        ImageStatics.ImageDetailTypes.High
                                    ));
                                }
                            }

                            // 处理完图片后添加到聊天消息中
                            if (item.Role == "user")
                            {
                                chatMessages.Add(ChatMessage.FromUser(hisvisionMessageContent));
                            }
                        }
                    }
                    else
                    {
                        if (item.Role == "user")
                            chatMessages.Add(ChatMessage.FromUser(item.Chat));
                        else
                            chatMessages.Add(ChatMessage.FromAssistant(item.Chat));
                    }

                    input += item.Chat;
                }

                if (chatDto.image_path.Count > 0)
                    chatMessages.Add(ChatMessage.FromUser(visionMessageContent));
                else
                    chatMessages.Add(ChatMessage.FromUser(promptHeadle));
            }

            var chatCompletionCreate = new ChatCompletionCreateRequest();
            chatCompletionCreate.Messages = chatMessages;
            if (mytools.Count > 0)
                chatCompletionCreate.Tools = mytools;
            //插件选择
            var tool_choice = myplugins.Where(x => x.MustHit == true).FirstOrDefault();
            if (tool_choice != null)
                if (!onknowledge)
                    chatCompletionCreate.ToolChoice = new ToolChoice
                    {
                        Type = "function",
                        Function = new ToolChoice.FunctionTool
                        {
                            Name = tool_choice.Pfunctionname
                        }
                    };
            chatCompletionCreate.Stream = true;
            chatCompletionCreate.Model = chatDto.aiModel;
            var sysmsg = string.Empty;
            var tikToken = TikToken.GetEncoding("cl100k_base");
            var (semaphore, cancellationToken) = _chatCancellationManager.GetOrCreateToken(chatDto.chatgroupid);
            if (channel == "ERNIE")
            {
                sysmsg = string.Empty;
                var fn = new BaiduResDto.FunctionCall();
                try
                {
                    await foreach (var responseContent in _baiduService.CallBaiduAI_Stream(chatCompletionCreate,
                                       openAiOptions, chatDto.chatgroupid, cancellationToken))
                    {
                        if (isFirstResponse)
                        {
                            firstTime = _systemService.CalculateTimeDifference(startTime, DateTime.Now).ToString("F1");
                            isFirstResponse = false;
                        }

                        if (responseContent != null && !string.IsNullOrEmpty(responseContent.Result))
                        {
                            sysmsg += responseContent.Result;
                            chatRes.message = responseContent.Result;
                            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                            Thread.Sleep(delay);
                        }

                        output += sysmsg;
                        await _financeService.UsageSaveRedis(chatId, Account, "assistant", sysmsg);
                        fn = responseContent.Function_Call;
                    }

                    if (fn != null)
                    {
                        var openaiFn = new FunctionCall();
                        _systemService.CopyPropertiesTo(fn, openaiFn);
                        var pluginResDto = new PluginResDto();
                        var ctsemoji = new CancellationTokenSource();
                        _aiServer.ExecuteFunctionWithLoadingIndicators(fn.Name, chatId, senMethod, ctsemoji.Token);
                        pluginResDto = await _workShop.RunPlugin(Account, openaiFn, chatId, senMethod, typeCode,
                            cancellationToken, chatDto.knowledgetopk, chatDto.knowledgereranker, chatDto.knowledgetopn);
                        if (!pluginResDto.doubletreating)
                        {
                            ctsemoji.Cancel();
                            sysmsg = await _aiServer.UnDoubletreating(pluginResDto, chatId, senMethod);
                            await _financeService.UsageSaveRedis(chatId, Account, "assistant", sysmsg);
                        }
                        else
                        {
                            ctsemoji.Cancel();
                            chatMessages.Add(ChatMessage.FromAssistant(fn.Thoughts));
                            //生成对话参数
                            input += pluginResDto.result;
                            chatMessages.Add(ChatMessage.FromUser(pluginResDto.result));
                            chatCompletionCreate.Messages = chatMessages;
                            chatCompletionCreate.Tools = null;
                            chatCompletionCreate.ToolChoice = null;
                            chatCompletionCreate.Stream = true;
                            chatCompletionCreate.Model = chatDto.aiModel;
                            await foreach (var responseContent in _baiduService.CallBaiduAI_Stream(chatCompletionCreate,
                                               openAiOptions, chatDto.chatgroupid, cancellationToken))
                                if (responseContent != null && !string.IsNullOrEmpty(responseContent.Result))
                                {
                                    sysmsg += responseContent.Result;
                                    chatRes.message = responseContent.Result;
                                    await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                                    Thread.Sleep(delay);
                                }
                        }

                        output += sysmsg;
                        await _financeService.UsageSaveRedis(chatId, Account, "assistant", sysmsg);
                        if (!string.IsNullOrEmpty(fn.Arguments))
                            output += fn.Arguments;
                    }
                }
                catch (OperationCanceledException)
                {
                    await _financeService.UsageSaveRedis(chatId, Account, "assistant",
                        sysmsg);
                    //await _systemService.WriteLog("工坊ERNIE输出取消", Dtos.LogLevel.Info, Account); //输出取消
                }
            }
            else
            {
                var completionResult = openAiService.ChatCompletion.CreateCompletionAsStream(chatCompletionCreate,
                    chatCompletionCreate.Model, true, cancellationToken);
                string json = JsonConvert.SerializeObject(chatCompletionCreate);
                var functionArguments = new Dictionary<int, string>();
                var fn = new FunctionCall();
                var pluginResDto = new PluginResDto();
                try
                {
                    await foreach (var responseContent in completionResult.WithCancellation(cancellationToken))
                    {
                        if (isFirstResponse)
                        {
                            firstTime = _systemService.CalculateTimeDifference(startTime, DateTime.Now).ToString("F1");
                            isFirstResponse = false;
                        }

                        if (responseContent.Successful)
                        {
                            var choice = responseContent.Choices.FirstOrDefault();
                            if (choice != null)
                                if (choice.Message != null)
                                {
                                    sysmsg += choice.Message.Content;
                                    output += choice.Message.Content;
                                    await _financeService.UsageSaveRedis(chatId, Account, "assistant", sysmsg);
                                    chatRes.message = choice.Message.Content;
                                    await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                                    var tools = choice.Message.ToolCalls;
                                    if (tools != null)
                                    {
                                        //函数并行待定......
                                        //for (int i = 0; i < tools.Count; i++)
                                        //{
                                        //    var toolCall = tools[i];
                                        //    var fn = toolCall.FunctionCall;
                                        //    if (fn != null)
                                        //    {
                                        //        if (!string.IsNullOrEmpty(fn.Name))
                                        //        {
                                        //            pluginResDto = await _workShop.RunPlugin(Account, fn);
                                        //        }
                                        //    }
                                        //}
                                        var toolCall = tools[0];
                                        fn = toolCall.FunctionCall;
                                    }

                                    if (fn != null)
                                    {
                                        var ctsemoji = new CancellationTokenSource();
                                        if (!string.IsNullOrEmpty(fn.Name))
                                        {
                                            await _aiServer.ExecuteFunctionWithLoadingIndicators(fn.Name, chatId,
                                                senMethod,
                                                ctsemoji.Token);
                                            pluginResDto = await _workShop.RunPlugin(Account, fn, chatId, senMethod,
                                                typeCode, cancellationToken, chatDto.knowledgetopk,
                                                chatDto.knowledgereranker, chatDto.knowledgetopn);
                                            if (!pluginResDto.doubletreating)
                                            {
                                                ctsemoji.Cancel();
                                                sysmsg = await _aiServer.UnDoubletreating(pluginResDto, chatId,
                                                    senMethod);
                                                await _financeService.UsageSaveRedis(chatId, Account, "assistant",
                                                    sysmsg, AIBotProEnum.HashFieldOperationMode.Append);
                                            }
                                            //反馈GPT函数执行结果
                                            else
                                            {
                                                ctsemoji.Cancel();
                                                //生成对话参数
                                                input += pluginResDto.result;
                                                chatMessages.Add(ChatMessage.FromUser(pluginResDto.result));
                                                chatCompletionCreate.Messages = chatMessages;
                                                chatCompletionCreate.Tools = null;
                                                chatCompletionCreate.ToolChoice = null;
                                                chatCompletionCreate.Stream = true;
                                                chatCompletionCreate.Model = chatDto.aiModel;
                                                completionResult =
                                                    openAiService.ChatCompletion.CreateCompletionAsStream(
                                                        chatCompletionCreate, chatCompletionCreate.Model, true,
                                                        cancellationToken);
                                                await foreach (var responseContent_sec in completionResult
                                                                   .WithCancellation(cancellationToken))
                                                {
                                                    if (responseContent_sec.Successful)
                                                    {
                                                        var choice_sec = responseContent_sec.Choices.FirstOrDefault();
                                                        if (choice_sec != null && choice_sec.Message != null)
                                                        {
                                                            sysmsg += choice_sec.Message.Content;
                                                            chatRes.message = choice_sec.Message.Content;
                                                            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                                                        }
                                                    }

                                                    Thread.Sleep(delay);
                                                }

                                                output += sysmsg;
                                                await _financeService.UsageSaveRedis(chatId, Account, "assistant",
                                                    sysmsg);
                                            }

                                            if (!string.IsNullOrEmpty(fn.Arguments))
                                                output += fn.Arguments;
                                        }
                                    }
                                }

                            Thread.Sleep(delay);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    await _financeService.UsageSaveRedis(chatId, Account, "assistant",
                        sysmsg);
                    //await _systemService.WriteLog("工坊OpenAI输出取消", Dtos.LogLevel.Info, Account); //输出取消
                }
            }

            //保存对话记录
            // 保存对话记录
            if (chatDto.image_path != null && chatDto.image_path.Count > 0)
            {
                foreach (var imagePath in chatDto.image_path)
                {
                    chatDto.msg +=
                        $@"aee887ee6d5a79fdcmay451ai8042botf1443c04<br /><img src=""{imagePath.Replace("wwwroot", "")}"" style=""max-width:50%;"" />";
                }
            }

            chatRes.message = "";
            chatRes.isfinish = true;
            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
            allTime = _systemService.CalculateTimeDifference(startTime, DateTime.Now).ToString("F1");
            sysmsg = await _financeService.UsageGetRedis(chatId, "assistant");
            await _aiServer.SaveChatHistory(Account, chatId, chatDto.msg, chatDto.msgid_u, chatDto.chatgroupid, "user",
                chatDto.aiModel, firstTime, allTime);
            await _aiServer.SaveChatHistory(Account, chatId, sysmsg, chatDto.msgid_g, chatDto.chatgroupid, "assistant",
                chatDto.aiModel, firstTime, allTime);
            if (!string.IsNullOrEmpty(sysmsg) && !useMyKey)
            {
                var freePlan = await _financeService.CheckFree(Account, chatDto.aiModel);
                if (freePlan.RemainCount > 0)
                {
                    await _financeService.UpdateFree(Account);
                    await _financeService.CreateUseLog(Account, chatDto.aiModel, tikToken.Encode(input).Count,
                        tikToken.Encode(output).Count, 0);
                }
                else
                {
                    await _financeService.CreateUseLogAndUpadteMoney(Account, chatDto.aiModel,
                        tikToken.Encode(input).Count, tikToken.Encode(output).Count);
                }
            }

            await _financeService.DeleteUsageRedis(chatId, "assistant");
        }
        catch (Exception e)
        {
            await ExceptionHandling(chatId, senMethod, e.Message);
        }
    }

    //助理GPT交互
    public async Task SendAssistantMessage(ChatDto chatDto)
    {
        var httpContext = Context.GetHttpContext();
        var token = string.Empty;
        token = httpContext?.Request.Query["access_token"];
        if (string.IsNullOrEmpty(token) || !_jwtTokenManager.isTokenValid(token) || string.IsNullOrEmpty(chatDto.ip))
        {
            // 如果没有令牌或者令牌无效或者ip为空，则断开连接
            Context.Abort();
            return;
        }

        //chatDto是否读取缓存
        if (!string.IsNullOrEmpty(chatDto.inputCacheKey))
        {
            var cache = await _redis.GetAsync(chatDto.inputCacheKey);
            await _redis.DeleteAsync(chatDto.inputCacheKey);
            if (!string.IsNullOrEmpty(cache))
            {
                chatDto = JsonConvert.DeserializeObject<ChatDto>(cache);
            }
        }

        //从token中获取账号信息
        var Account = string.Empty;
        if (!chatDto.isbot)
            Account = _jwtTokenManager.ValidateToken(token).Identity.Name;
        else
            Account = "robot_AIBOT";
        var user = _usersService.GetUserData(Account);
        var chatId = string.Empty;
        var threadId = chatDto.threadid;
        var newChat = false;
        if (string.IsNullOrEmpty(chatDto.chatid))
        {
            chatId = Guid.NewGuid().ToString().Replace("-", ""); //创建chatid头部
            chatId = $"{chatId}U{Account}IP{chatDto.ip}";
            chatDto.chatid = chatId;
            newChat = true;
        }
        else
        {
            chatId = chatDto.chatid;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
        var chatRes = new ChatRes();
        chatRes.chatid = chatId;
        var senMethod = "ReceiveAssistantMessage";
        if (chatDto.isbot)
            senMethod = "ReceiveAssistantMessage_bot";
        //回应客户端就绪状态
        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
        var promptHeadle = chatDto.msg;
        //对话前的检查
        if (!await _usersService.ChatHubBeforeCheck(chatDto, Account, senMethod, chatId))
            return;
        //根据账号查询Assistans
        var assistant = _assistantService.GetAssistantGPTs(Account);
        if (assistant.Count == 0)
        {
            chatRes.message = "您似乎还没有创建助理，您可以<a href='/AssistantGPT/AssistantSetting'>点击这里</a>前往创建";
            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
            chatRes.message = "";
            chatRes.isfinish = true;
            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
            return;
        }

        var assisId = assistant.First().AssisId;
        try
        {
            if (string.IsNullOrEmpty(threadId)) //新对话
            {
                //创建线程
                threadId = await _assistantService.CreateThread();
                chatRes.threadid = threadId;
                //chatRes.message = $"线程已创建【{threadId}】，程序继续运行";
                await Clients.Group(chatId).SendAsync(senMethod, chatRes);
            }

            chatRes.threadid = string.Empty;
            //向线程添加消息
            var msgId = await _assistantService.AddMessage(threadId, promptHeadle);
            var sysmsg = string.Empty;
            var fileids = new List<string>();
            await foreach (var responseContent in _assistantService.RunThread(threadId, assisId, Account))
            {
                sysmsg += responseContent;
                chatRes.message = responseContent.message;
                await Clients.Group(chatId).SendAsync(senMethod, chatRes);
                if (responseContent.file_ids != null && responseContent.file_ids.Count > 0)
                    for (var i = 0; i < responseContent.file_ids.Count; i++)
                        if (!fileids.Contains(responseContent.file_ids[i]))
                            fileids.Add(responseContent.file_ids[i]);
                //Thread.Sleep(50);
            }

            foreach (var item in fileids)
            {
                chatRes.file_id = item;
                chatRes.message = "";
                await Clients.Group(chatId).SendAsync(senMethod, chatRes);
            }

            var tikToken = TikToken.GetEncoding("cl100k_base");
            chatRes.message = "";
            chatRes.isfinish = true;
            await Clients.Group(chatId).SendAsync(senMethod, chatRes);
        }
        catch (Exception e)
        {
            await ExceptionHandling(chatId, senMethod, e.Message);
        }
    }

    // 重写OnConnectedAsync方法
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var token = string.Empty;
        token = httpContext?.Request.Query["access_token"];
        if (string.IsNullOrEmpty(token) || !_jwtTokenManager.isTokenValid(token))
        {
            // 如果没有令牌或者令牌无效，则断开连接，并抛出异常要求登录
            Context.Abort();
            throw new Exception("连接失败");
        }

        await base.OnConnectedAsync();
    }

    private static void SetUpParams(dynamic obj, ChatDto chatDto)
    {
        obj.Temperature = chatDto.temperature;
        //obj.TopP = chatDto.topp;
        obj.FrequencyPenalty = chatDto.frequency;
        obj.PresencePenalty = chatDto.presence;
        obj.MaxTokens = chatDto.maxtokens;
    }

    private async Task ExceptionHandling(string chatId, string senMethod, string errorMsg)
    {
        var chatRes = new ChatRes();
        await _redis.DeleteAsync($"{chatId}_process");
        chatRes.message = $"糟糕！出错了！错误原因：【{errorMsg}】,刷新页面或重试一次吧😢";
        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
        chatRes.message = "";
        chatRes.isfinish = true;
        await Clients.Group(chatId).SendAsync(senMethod, chatRes);
        await _systemService.WriteLog($"{senMethod}:{errorMsg}", LogLevel.Error, $"chatId:{chatId}");
    }
}