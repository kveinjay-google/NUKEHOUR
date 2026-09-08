#import <Foundation/Foundation.h>
#import <MetricKit/MetricKit.h>

@interface OpenRAMetricKitSink : NSObject <MXMetricManagerSubscriber>
@property(nonatomic, copy) NSString *directory;
@end

@implementation OpenRAMetricKitSink
- (void)persist:(NSArray<MXDiagnosticPayload *> *)payloads
{
	NSFileManager *fm = NSFileManager.defaultManager;
	[fm createDirectoryAtPath:self.directory withIntermediateDirectories:YES attributes:nil error:nil];
	for (MXDiagnosticPayload *payload in payloads)
	{
		NSData *json = payload.JSONRepresentation;
		NSString *name = [NSString stringWithFormat:@"metrickit-%@.json", NSUUID.UUID.UUIDString];
		[json writeToFile:[self.directory stringByAppendingPathComponent:name] atomically:YES];
	}
}

- (void)didReceiveDiagnosticPayloads:(NSArray<MXDiagnosticPayload *> *)payloads
{
	[self persist:payloads];
}
@end

static OpenRAMetricKitSink *sink;

void OpenRAStartMetricKitDiagnostics(const char *path)
{
	if (@available(iOS 14.0, *))
	{
		sink = [OpenRAMetricKitSink new];
		sink.directory = [NSString stringWithUTF8String:path];
		[sink persist:MXMetricManager.sharedManager.pastDiagnosticPayloads];
		[MXMetricManager.sharedManager addSubscriber:sink];
	}
}

void OpenRAStopMetricKitDiagnostics(void)
{
	if (@available(iOS 14.0, *))
		if (sink != nil)
			[MXMetricManager.sharedManager removeSubscriber:sink];
	sink = nil;
}
