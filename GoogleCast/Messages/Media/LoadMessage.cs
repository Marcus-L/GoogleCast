﻿using System.Runtime.Serialization;

namespace GoogleCast.Messages.Media
{
    /// <summary>
    /// Load message
    /// </summary>
    [DataContract]
    class LoadMessage : SessionMessage
    {
        /// <summary>
        /// Gets or sets the media
        /// </summary>
        [DataMember(Name = "media")]
        public Models.Media.Media Media { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the media must be played directly or not
        /// </summary>
        [DataMember(Name = "autoplay")]
        public bool AutoPlay { get; set; }
    }
}
