﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    public partial class AImodel
    {
        [Key]
        public int Id { get; set; }
        [StringLength(100)]
        public string ModelNick { get; set; }
        [StringLength(100)]
        public string ModelName { get; set; }
        [StringLength(100)]
        public string BaseUrl { get; set; }
        [StringLength(100)]
        public string ApiKey { get; set; }
        public bool? VisionModel { get; set; }
        public int? Seq { get; set; }
    }
}