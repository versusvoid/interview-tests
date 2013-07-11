{-# LANGUAGE BangPatterns, OverloadedStrings #-}
import           Data.List
import qualified Data.ByteString as B()
import           Network.Socket hiding (send, sendTo, recv, recvFrom)
import           Network.Socket.ByteString
import           Data.Maybe as MB
import           System.Environment as Env(getArgs)
import           System.Exit(exitFailure)
import           System.Time
import           Control.Monad
import           Control.Concurrent(threadDelay)

data NodeHandle = 
     NodeHandle {nhSock    :: Socket,
                 nhId      :: Int,
                 nhNodes   :: [(Int, SockAddr)],
                 nhKing    :: Int
                } 

microSecDelay = 1000*1000

delayDiff = normalizeTimeDiff $ TimeDiff{tdPicosec = microSecDelay*1000000*4}

openSock = do 
    addrs <- fmap (map $ groupBy (\_ c -> c /= ':')) getArgs
    when (length addrs < 2) exitFailure
    let ![myId, myHost, myPort] = head addrs
    addrinfos <- getAddrInfo Nothing (Just myHost) (Just myPort)
    let serveraddr = head addrinfos
    sock <- socket (addrFamily serveraddr) Datagram defaultProtocol
    setSocketOption sock RecvTimeOut (fromInteger $ microSecDelay `div` 1000)

    nodes <- forM (tail addrs) $ \[index, ':':host, ':':port'] -> do
                hostId <- inet_addr host
                return $! (read index, SockAddrInet (PortNum $ read port') hostId) 
    return $! NodeHandle sock (read myId) nodes 0

main = do
    nh <- openSock
    elect nh

addrToId addr = 
    fromJust . 
    foldl (\k (i,a) -> 
             if a == addr then Just i else k) Nothing .
    nhNodes

elect nh
    | nhId nh == foldl' (\i (j,_) -> i `max` j) 0 (nhNodes nh)
        = iAmKing nh
    | otherwise = do
        forM_ (nhNodes nh) $ \(i,addr) ->
            when (i > nhId nh) $ do
               void $ sendTo (nhSock nh) "ALIVE?" addr
        (message, _) <- recvFrom (nhSock nh) 20
        case message of
             "FINETHANKS" -> waitKing nh
             "" -> iAmKing nh
             _  -> undefined

waitKing nh = do
    (message, addr) <- recvFrom (nhSock nh) 20
    case message of
         "IAMTHEKING" -> 
            getClockTime >>= 
            (processQueue nh{nhKing = addrToId addr nh})
         _ -> elect nh

iAmKing nh = do
    forM_ (nhNodes nh) $ \(i,addr) ->
        when (i /= nhId nh) $ do
           void $ sendTo (nhSock nh) "IAMTHEKING" addr
    time <- getClockTime
    processQueue (nh{nhKing = nhId nh}) time

processQueue nh lastPongTime = getClockTime >>=
    \beginTime ->
      if beginTime > addToClockTime delayDiff lastPongTime
         then elect nh
         else do
           let sock = nhSock nh
               nodes = nhNodes nh
               king = nhKing nh
               myId = nhId nh
           when (myId /= king) $ do
               void $ sendTo (sock) "PING" (fromJust $ lookup king nodes)
           (message, addr) <- recvFrom (nhSock nh) 20
           case message of
                "PONG"   -> getClockTime
                        >>= (waitDiff beginTime) 
                        >>= (processQueue nh)
                "ALIVE?" -> (sendTo sock "FINETHANKS" addr) >> elect nh
                ""       -> getClockTime
                        >>= (waitDiff beginTime)
                        >>  processQueue nh lastPongTime

waitDiff (TOD s1 ps1) c@(TOD s2 ps2)
    | microDiff > microSecDelay = return c
    | otherwise = threadDelay (fromInteger $ (microSecDelay - microDiff)) 
               >> return c
    where
        microDiff = (s2 - s2)*1000000 + (ps2 - ps1) `div` 1000000
