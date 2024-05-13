
using AVBufferRef = FFmpeg.AutoGen.Abstractions.AVBufferRef;
using AVCodec = FFmpeg.AutoGen.Abstractions.AVCodec;
using AVCodecContext = FFmpeg.AutoGen.Abstractions.AVCodecContext;
using AVFormatContext = FFmpeg.AutoGen.Abstractions.AVFormatContext;
using AVFrame = FFmpeg.AutoGen.Abstractions.AVFrame;
using AVMediaType = FFmpeg.AutoGen.Abstractions.AVMediaType;
using AVPacket = FFmpeg.AutoGen.Abstractions.AVPacket;
using AVPixelFormat = FFmpeg.AutoGen.Abstractions.AVPixelFormat;
using DynamicallyLoadedBindings = FFmpeg.AutoGen.Bindings.DynamicallyLoaded.DynamicallyLoadedBindings;
using ffmpeg = FFmpeg.AutoGen.Abstractions.ffmpeg;
using SwsContext = FFmpeg.AutoGen.Abstractions.SwsContext;



namespace VideoHandler;


public unsafe class VideoStreamHandler
{
    private AVFormatContext* pFormatContext = null;
    private AVCodecContext* pCodecContext = null;
    private AVFrame* pFrame = null;

    private AVFrame*[] pFrameRGBs = new AVFrame*[5];
    private int maxFrames = 5;
    private int currIdx = 0;
    
    private SwsContext* swsCtx = null;
    private int videoStreamIndex = -1;
    private AVBufferRef* hwDeviceCtx = null;

    public int totalFrames;

    public static void Init()
    {
        Console.WriteLine("Current directory: " + Environment.CurrentDirectory);
        Console.WriteLine("Running in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");
        FFmpegBinariesHelper.RegisterFFmpegBinaries();
        DynamicallyLoadedBindings.Initialize();
        Console.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");
    }

    public void Initialize(string filePath)
    {
        Console.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");
        ffmpeg.avdevice_register_all();
        ffmpeg.avformat_network_init();
        
        AVFormatContext* pFormatContextt = null;

        pFormatContextt = ffmpeg.avformat_alloc_context();
        ffmpeg.avformat_open_input(&pFormatContextt, filePath, null, null);
        ffmpeg.avformat_find_stream_info(pFormatContextt, null);

        pFormatContext = pFormatContextt;
        

        videoStreamIndex = -1;
        for (int i = 0; i < pFormatContextt->nb_streams; i++)
        {
            if (pFormatContextt->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                videoStreamIndex = i;
                break;
            }
        }

        if (videoStreamIndex == -1) throw new ApplicationException("Video stream not found.");

        pCodecContext = ffmpeg.avcodec_alloc_context3(null);
        ffmpeg.avcodec_parameters_to_context(pCodecContext, pFormatContextt->streams[videoStreamIndex]->codecpar);
        
        AVCodec* codec = ffmpeg.avcodec_find_decoder(pCodecContext->codec_id);
        if (codec == null) throw new ApplicationException("Codec not found.");

        if (ffmpeg.avcodec_open2(pCodecContext, codec, null) < 0)
            throw new ApplicationException("Could not open the codec.");
        
        
        pFrame = ffmpeg.av_frame_alloc();

        int width = pCodecContext->width;
        int height = pCodecContext->height;



        swsCtx = ffmpeg.sws_getContext(width, height, pCodecContext->pix_fmt,
            width, height, AVPixelFormat.AV_PIX_FMT_RGB24,
            ffmpeg.SWS_BILINEAR, null, null, null);


        totalFrames = (int) (((pFormatContext->duration) / 1000000d) *
                      (pFormatContext->streams[0]->avg_frame_rate.num /
                       (double)pFormatContext->streams[0]->avg_frame_rate.den)) - 2;
    }

    public AVFrame* GetNextFrame()
    {
        AVPacket packet;
        if (pCodecContext->frame_num >= totalFrames)
        {
            return null;
        }
        
        while (ffmpeg.av_read_frame(pFormatContext, &packet) >= 0)
        {
            if (packet.stream_index == videoStreamIndex)
            {
                if (ffmpeg.avcodec_send_packet(pCodecContext, &packet) == 0)
                {
                    if (ffmpeg.avcodec_receive_frame(pCodecContext, pFrame) == 0)
                    {
                        ffmpeg.av_packet_unref(&packet);


                        if (pCodecContext->pix_fmt == AVPixelFormat.AV_PIX_FMT_RGB24)
                        {
                            return pFrame;
                        }
                        var pFrameRGB = GetAVFrame();
                        
                        ffmpeg.sws_scale(swsCtx, pFrame->data, pFrame->linesize, 0, pCodecContext->height, pFrameRGB->data, pFrameRGB->linesize);

                        return pFrameRGB;
                    }
                }
            }
            ffmpeg.av_packet_unref(&packet);
        }
        return null;  
    }

    private AVFrame* GetAVFrame()
    {
        AVFrame* avFrame;
        
        if (pFrameRGBs[currIdx] != null)
        {
            var pFramePtr = pFrameRGBs[currIdx];
            ffmpeg.av_frame_free(&pFramePtr);
        }

        avFrame = ffmpeg.av_frame_alloc();
        
        avFrame->format = (int)AVPixelFormat.AV_PIX_FMT_RGB24;
        int width = pCodecContext->width;
        int height = pCodecContext->height;
        avFrame->width = width;
        avFrame->height = height;
        
        ffmpeg.av_frame_get_buffer(avFrame, 0);

        pFrameRGBs[currIdx] = avFrame;
        
        currIdx++;
        currIdx %= maxFrames;

        return avFrame;
    }

    public void Cleanup()
    {
        var pFramePtr = pFrame;
        ffmpeg.av_frame_free(&pFramePtr);

        var pCodecContextPtr = pCodecContext;
        ffmpeg.avcodec_close(pCodecContextPtr);

        var hwDeviceCtxPtr = hwDeviceCtx;
        ffmpeg.av_buffer_unref(&hwDeviceCtxPtr);

        var pFormatContextPtr = pFormatContext;
        ffmpeg.avformat_close_input(&pFormatContextPtr);
    }
}