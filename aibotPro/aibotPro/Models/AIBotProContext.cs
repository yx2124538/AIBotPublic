﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace aibotPro.Models
{
    public partial class AIBotProContext : DbContext
    {
        public AIBotProContext()
        {
        }

        public AIBotProContext(DbContextOptions<AIBotProContext> options)
            : base(options)
        {
        }

        public virtual DbSet<AIdraw> AIdraws { get; set; }
        public virtual DbSet<AIdrawRe> AIdrawRes { get; set; }
        public virtual DbSet<AImodel> AImodels { get; set; }
        public virtual DbSet<AImodelsUserSeq> AImodelsUserSeqs { get; set; }
        public virtual DbSet<APIKEY> APIKEYs { get; set; }
        public virtual DbSet<Admin> Admins { get; set; }
        public virtual DbSet<AssistantFile> AssistantFiles { get; set; }
        public virtual DbSet<AssistantGPT> AssistantGPTs { get; set; }
        public virtual DbSet<AssistantModelPrice> AssistantModelPrices { get; set; }
        public virtual DbSet<Card> Cards { get; set; }
        public virtual DbSet<ChatHistory> ChatHistories { get; set; }
        public virtual DbSet<ChatSetting> ChatSettings { get; set; }
        public virtual DbSet<EasyPaySetting> EasyPaySettings { get; set; }
        public virtual DbSet<ErrorBilling> ErrorBillings { get; set; }
        public virtual DbSet<FilesLib> FilesLibs { get; set; }
        public virtual DbSet<Good> Goods { get; set; }
        public virtual DbSet<IPlook> IPlooks { get; set; }
        public virtual DbSet<IPlook_Stats_View> IPlook_Stats_Views { get; set; }
        public virtual DbSet<Knowledge> Knowledges { get; set; }
        public virtual DbSet<KnowledgeList> KnowledgeLists { get; set; }
        public virtual DbSet<KnowledgeType> KnowledgeTypes { get; set; }
        public virtual DbSet<ModelPrice> ModelPrices { get; set; }
        public virtual DbSet<Notice> Notices { get; set; }
        public virtual DbSet<OpenAPIModelSetting> OpenAPIModelSettings { get; set; }
        public virtual DbSet<Order> Orders { get; set; }
        public virtual DbSet<Plugin> Plugins { get; set; }
        public virtual DbSet<PluginsCookie> PluginsCookies { get; set; }
        public virtual DbSet<PluginsHeader> PluginsHeaders { get; set; }
        public virtual DbSet<PluginsInstall> PluginsInstalls { get; set; }
        public virtual DbSet<PluginsJsonPr> PluginsJsonPrs { get; set; }
        public virtual DbSet<PluginsParam> PluginsParams { get; set; }
        public virtual DbSet<RoleChat> RoleChats { get; set; }
        public virtual DbSet<RoleSetting> RoleSettings { get; set; }
        public virtual DbSet<Share> Shares { get; set; }
        public virtual DbSet<ShareLog> ShareLogs { get; set; }
        public virtual DbSet<SignIn> SignIns { get; set; }
        public virtual DbSet<SystemCfg> SystemCfgs { get; set; }
        public virtual DbSet<SystemLog> SystemLogs { get; set; }
        public virtual DbSet<SystemPlugin> SystemPlugins { get; set; }
        public virtual DbSet<SystemPluginsInstall> SystemPluginsInstalls { get; set; }
        public virtual DbSet<TxOrder> TxOrders { get; set; }
        public virtual DbSet<UISetting> UISettings { get; set; }
        public virtual DbSet<UseUpLog> UseUpLogs { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserPrompt> UserPrompts { get; set; }
        public virtual DbSet<UserSetting> UserSettings { get; set; }
        public virtual DbSet<VIP> VIPs { get; set; }
        public virtual DbSet<WorkFlow> WorkFlows { get; set; }
        public virtual DbSet<WorkShopAIModel> WorkShopAIModels { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see http://go.microsoft.com/fwlink/?LinkId=723263.
                optionsBuilder.UseSqlServer("Data Source=45.137.10.185;Initial Catalog=AIBotPro;Persist Security Info=True;User ID=sa;Password=Qq2424020953@Google.com;Encrypt=True");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AImodel>(entity =>
            {
                entity.Property(e => e.VisionModel).HasDefaultValueSql("((0))");
            });

            modelBuilder.Entity<IPlook_Stats_View>(entity =>
            {
                entity.ToView("IPlook_Stats_View");
            });

            modelBuilder.Entity<ModelPrice>(entity =>
            {
                entity.Property(e => e.OnceFee).HasDefaultValueSql("((0))");

                entity.Property(e => e.VipOnceFee).HasDefaultValueSql("((0))");
            });

            modelBuilder.Entity<PluginsInstall>(entity =>
            {
                entity.Property(e => e.MustHit).HasDefaultValueSql("((0))");
            });

            modelBuilder.Entity<UISetting>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}