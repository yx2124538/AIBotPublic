﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("ChatSetting")]
    public partial class ChatSetting
    {
        [Key]
        public int Id { get; set; }
        [StringLength(500)]
        public string ChatSettingKey { get; set; }
        public string ChatSettingValue { get; set; }
        [StringLength(200)]
        public string Account { get; set; }
    }
}