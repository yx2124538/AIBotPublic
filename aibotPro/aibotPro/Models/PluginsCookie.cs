﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    public partial class PluginsCookie
    {
        [Key]
        public int Id { get; set; }
        [StringLength(50)]
        public string CkCode { get; set; }
        [StringLength(500)]
        public string CkName { get; set; }
        [StringLength(500)]
        public string CkValue { get; set; }
    }
}