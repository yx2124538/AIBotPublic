﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("UISetting")]
    public partial class UISetting
    {
        [Key]
        public int Id { get; set; }
        [StringLength(50)]
        public string Account { get; set; }
        [StringLength(50)]
        public string SettingKey { get; set; }
        public string SettingValue { get; set; }
    }
}