using System.Threading.Tasks;

namespace Unobtanium.Web.Proxy.Events;

/// <summary>
///     A generic asynchronous event handler used by the proxy.
/// </summary>
/// <typeparam name="TEventArgs">Event argument type.</typeparam>
/// <param name="sender">The proxy server instance.</param>
/// <param name="e">The event arguments.</param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
public delegate Task AsyncEventHandler<in TEventArgs> ( object sender, TEventArgs e, CancellationToken cancellationToken );

/// <summary>
///     A generic asynchronous event handler used by the proxy, with a return type.
/// </summary>
/// <typeparam name="TEventArgs">Event argument type.</typeparam>
/// <typeparam name="TResponse">What should this return.</typeparam>
/// <param name="sender">The proxy server instance.</param>
/// <param name="e">The event arguments.</param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
public delegate Task<TResponse> AsyncEventHandler<in TEventArgs, TResponse> ( object sender, TEventArgs e, CancellationToken cancellationToken );
