# syntax=docker/dockerfile:1.7

# A Linux desktop (XFCE) for running the Flowery.NET Gallery and other Avalonia apps, viewed in a browser through noVNC.
#
# The image holds no .NET SDK or app: publish the app self-contained for linux-x64 and mount it at /opt/app
# (scripts/run-linux-desktop.ps1 does both). Build it with:
#   docker build -t flowery-linux-desktop docker/linux-desktop
FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

# XFCE as the desktop, TigerVNC as its X server, noVNC to show it in a browser. Avalonia needs X11 with its input and
# cursor extensions, fontconfig with fonts (an emoji font too, the Gallery shows emoji), and ICU for globalization; Mesa gives OpenGL without a GPU. xwd with netpbm
# takes PNG screenshots and xdotool drives input, for checks from outside the container.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        xfce4 xfce4-terminal dbus-x11 \
        tigervnc-standalone-server tigervnc-tools novnc python3-websockify \
        libx11-6 libice6 libsm6 libxi6 libxcursor1 libxrandr2 libxext6 libxrender1 \
        libfontconfig1 fonts-dejavu-core fonts-noto-core fonts-noto-color-emoji libicu72 \
        libgl1 libegl1 libgl1-mesa-dri \
        x11-apps netpbm xdotool procps ca-certificates \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --shell /bin/bash flowery

# http://localhost:6080/ opens the desktop instead of a directory listing.
RUN echo '<!DOCTYPE html><meta http-equiv="refresh" content="0; url=vnc.html?autoconnect=true&amp;resize=scale">' \
        > /usr/share/novnc/index.html

COPY start-desktop.sh /usr/local/bin/start-desktop.sh
# The script may arrive with Windows line endings from a checkout on Windows.
RUN sed -i 's/\r$//' /usr/local/bin/start-desktop.sh && chmod +x /usr/local/bin/start-desktop.sh

USER flowery
WORKDIR /home/flowery
# English unless the run passes another culture as LANG (scripts/run-linux-desktop.ps1 passes the one of Windows).
ENV DISPLAY=:1 \
    LANG=en_US.UTF-8 \
    APP_PATH=/opt/app/Flowery.NET.Gallery.Desktop

# 6080: noVNC in a browser; 5901: VNC for a native viewer.
EXPOSE 6080 5901

ENTRYPOINT ["/usr/local/bin/start-desktop.sh"]
