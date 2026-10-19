using Maydel.Application.Common.Messaging;

namespace Maydel.Application.Website.SavePost;

/// <summary>Creates a draft post (<c>Id</c> null) or edits one. Published posts keep their web address.</summary>
public sealed record SavePostCommand(Guid? Id, PostInput Post) : ICommand<PostDto>;

/// <summary>Publishes (shows) or unpublishes (hides) a post.</summary>
public sealed record PublishPostCommand(Guid Id, bool Publish) : ICommand<PostDto>;

public sealed record DeletePostCommand(Guid Id) : ICommand;

/// <summary>Sets or (with null) removes the post's picture: re-encoded JPEG up to 1600 px.</summary>
public sealed record SetPostImageCommand(Guid Id, byte[]? File) : ICommand<PostDto>;
