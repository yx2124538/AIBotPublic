﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("AImodelsUserSeq")]
    public partial class AImodelsUserSeq
    {
        [Key]
        public int Id { get; set; }
        [StringLength(500)]
        public string Account { get; set; }
        [StringLength(1000)]
        public string ModelNick { get; set; }
        [StringLength(1000)]
        public string ModelName { get; set; }
        public int? Seq { get; set; }
    }
}